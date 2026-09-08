using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Admin.Dtos;
using Ticketing.Application.Common;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Application.Admin;

/// <summary>
/// 暫停售票與一鍵重置。
///
/// 兩個操作都遵守跟購票**同一套**協定，只是鎖的東西不同：
/// 購票是「場次共享 → 買家更新」，管理是「場次排他」。
/// 對同一個場次互斥，所以「重置撞上正在買票」有明確的先後（設計文件 14 第 6 節）。
///
/// 去重紀錄存在 <c>AdminAudits</c> 而不是 <c>IdempotencyRecords</c>——後者會被重置自己刪掉。
/// </summary>
public sealed class AdminService(
    IUnitOfWork uow,
    IBookingWriteGate gate,
    IAdminDao admin,
    IPerformanceRepository performances,
    TimeProvider clock,
    ILogger<AdminService> logger) : IAdminService
{
    private const string PauseAction = "SetSalesPaused";
    private const string ResetAction = "Reset";

    /// <summary>重置後最早的一場固定落在「今天（UTC 日期）＋ 14 天」之後。</summary>
    private const int TargetLeadDays = 14;

    // ── 暫停／恢復 ────────────────────────────────────────────────────

    public async Task<AdminOperationResponse> SetSalesPausedAsync(Guid actorId, int performanceId,
        PauseSalesRequest request, Guid operationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fingerprint = Fingerprint("PATCH", "/admin/performances/{id}",
            performanceId.ToString(CultureInfo.InvariantCulture),
            new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["isSalesPaused"] = request.IsSalesPaused
            });

        var response = await uow.ExecuteInTransactionAsync(async token =>
        {
            // 一開始就取排他鎖，**不先取共享再升級**——那是死結的經典來源
            if (!await gate.EnterPerformanceWriteAsync(performanceId, token))
                throw new BookingRuleException(ErrorCode.NotFound, "找不到這個場次");

            if (await FindReplayAsync(operationId, actorId, PauseAction, fingerprint, token) is { } replay)
                return replay;

            var now = clock.GetUtcNow();

            // 管理者看得到未上架的場次，所以用 GetAsync 不是 GetPublishedAsync
            var performance = await performances.GetAsync(performanceId, token)
                ?? throw new BookingRuleException(ErrorCode.NotFound, "找不到這個場次");

            if (request.IsSalesPaused) performance.PauseSales();
            else performance.ResumeSales();

            var json = PublicJson.Serialize(new PauseSalesResult(performanceId, performance.IsSalesPaused));
            admin.AddAudit(actorId, PauseAction, operationId, fingerprint,
                           detailsJson: null, resultJson: json, now);

            await uow.SaveChangesAsync(token);
            return new AdminOperationResponse(json, IsReplay: false);
        }, ct);

        if (!response.IsReplay)
            logger.LogInformation("SalesPauseChanged {PerformanceId} {Paused}", performanceId, request.IsSalesPaused);

        return response;
    }

    // ── 重置 ──────────────────────────────────────────────────────────

    public async Task<AdminOperationResponse> ResetAsync(Guid actorId, ResetRequest request,
        Guid operationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fingerprint = Fingerprint("POST", "/admin/reset", target: "",
            new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["confirmation"] = request.Confirmation
            });

        // commit 之後要記的數字。委派可能被 ExecutionStrategy 重跑，所以每次都覆蓋，
        // 最後一次成功的那次才算數；重播時它會是 null，下面的 !IsReplay 已經擋掉。
        ResetResult? committed = null;

        var response = await uow.ExecuteInTransactionAsync(async token =>
        {
            // 依 Id 升序逐筆取得**所有**場次的排他鎖。全部拿到才能開始改資料——
            // 不能先清資料再鎖，那樣中間仍有人買得到票
            await gate.EnterAllPerformancesWriteAsync(token);

            if (await FindReplayAsync(operationId, actorId, ResetAction, fingerprint, token) is { } replay)
                return replay;   // 重播時不重算日期、不再 reset 一次

            var now = clock.GetUtcNow();

            var counts = await admin.CountPurchasesAsync(token);
            var earliest = await admin.GetEarliestPerformanceStartAsync(token)
                ?? throw new BookingRuleException(ErrorCode.NotFound, "沒有任何場次可以重置");

            // 順序有意義：先把 Seats.HoldId 清掉，後面刪 SeatHolds 才不會撞外鍵
            var seatsReleased = await admin.ReleaseAllSeatsAsync(token);
            await admin.DeleteAllPurchaseDataAsync(token);

            // 只往後搬，不往前：已經在很遠未來的場次不該被拉近
            var shiftDays = Math.Max(0, (now.UtcDateTime.Date.AddDays(TargetLeadDays)
                                         - earliest.UtcDateTime.Date).Days);
            var salesOpensAt = now.AddDays(-1);
            await admin.ResetPerformanceScheduleAsync(shiftDays, salesOpensAt, token);

            var result = new ResetResult(counts.Orders, counts.Holds, seatsReleased,
                                         earliest.AddDays(shiftDays));
            var json = PublicJson.Serialize(result);
            committed = result;

            admin.AddAudit(actorId, ResetAction, operationId, fingerprint,
                           detailsJson: PublicJson.Serialize(new { shiftDays, salesOpensAtUtc = salesOpensAt }),
                           resultJson: json, now);

            await uow.SaveChangesAsync(token);
            return new AdminOperationResponse(json, IsReplay: false);
        }, ct);

        // 事件名照設計文件 10 第 3 節的固定清單：AdminReset {ActorId} {OrdersDeleted}
        // {HoldsDeleted}，再多帶釋放席次。命名規則見設計文件 12 第 3 節——
        // 那些只存在於設計文件、用來界定範圍的字眼，不得出現在程式、UI、log 或雲端資源名稱裡；
        // log 事件名同樣算數，CI 有一道 grep 在守（.github/workflows/ci.yml 的 naming-lint）。
        if (!response.IsReplay && committed is { } r)
            logger.LogWarning("AdminReset {ActorId} {OrdersDeleted} {HoldsDeleted} {SeatsReleased}",
                              actorId, r.OrdersDeleted, r.HoldsDeleted, r.SeatsReleased);

        return response;
    }

    // ── 去重 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 已經做過的同一個操作 → 回原本存下來的結果，**不改任何資料、也不再寫一筆稽核**。
    ///
    /// 三個欄位都要比對：同一個 key 換一個管理者、換一種操作、或換 body，都是誤用而不是重送。
    /// </summary>
    private async Task<AdminOperationResponse?> FindReplayAsync(Guid operationId, Guid actorId,
        string action, byte[] fingerprint, CancellationToken ct)
    {
        var existing = await admin.FindOperationAsync(operationId, ct);
        if (existing is null) return null;

        if (existing.ActorId != actorId
            || !string.Equals(existing.Action, action, StringComparison.Ordinal)
            || !existing.RequestHash.AsSpan().SequenceEqual(fingerprint))
        {
            // 不外露原操作的內容——只說「這個 key 已經用在別的事情上了」
            throw new BookingRuleException(ErrorCode.IdempotencyKeyReuse,
                                           "相同的 Idempotency-Key 已經用在另一個管理操作");
        }

        return new AdminOperationResponse(existing.ResultJson, IsReplay: true);
    }

    /// <summary>與購票端同一套正規化規則（設計文件 06 第 7 節）：版本、方法、路由、目標、body。</summary>
    private static byte[] Fingerprint(string method, string route, string target,
                                      SortedDictionary<string, object?> body)
    {
        var canonical = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["body"] = body,
            ["method"] = method,
            ["route"] = route,
            ["target"] = target,
            ["version"] = 1
        };

        return SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical)));
    }
}
