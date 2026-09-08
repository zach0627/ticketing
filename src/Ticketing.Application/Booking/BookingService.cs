using Microsoft.Extensions.Logging;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Booking.Dtos;
using Ticketing.Application.Common;
using Ticketing.Application.Orders;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;
using Ticketing.Domain.Orders;

namespace Ticketing.Application.Booking;

/// <summary>
/// 這個專案最重要的一個類別：**同一席不會賣給兩個人、同一個人不會超過上限、
/// 同一個 key 不會買兩次**。
///
/// 三件事都不是靠「先查再寫」達成的（那在併發下必然有縫），而是靠一組固定的協定
/// （設計文件 06）：
///
/// <code>
/// 場次共享 gate → 買家更新 gate → 取 now → 查冪等 → 重新載入驗證 → 條件 UPDATE 比對列數
/// </code>
///
/// 順序有意義，每一步都不能省。詳細理由在走讀文件；這裡只保留為什麼那樣寫的短註解。
/// </summary>
public sealed class BookingService(
    IUnitOfWork uow,
    IBookingWriteGate gate,
    IPerformanceRepository performances,
    ISeatRepository seats,
    ISeatHoldRepository holds,
    IOrderRepository orders,
    IdempotencyGuard idempotency,
    TimeProvider clock,
    ILogger<BookingService> logger) : IBookingService
{
    /// <summary>保留期限。定義在設計文件 02 第 1 節，程式裡只有這一個地方寫。</summary>
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(5);

    private const string Currency = "TWD";

    // ── 建立保留 ──────────────────────────────────────────────────────

    public async Task<IdempotencyResponse> CreateHoldAsync(Guid buyerId, int performanceId,
        CreateHoldRequest request, string idempotencyKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ⚠️ 指紋在交易**外面**算：ExecutionStrategy 會重跑整個委派，
        // 委派裡只能有「每次重跑都要重算」的東西。key 與指紋是不變的輸入。
        ValidateShape(request);
        var fingerprint = IdempotencyCommand.CreateHold(performanceId, request);

        var result = await uow.ExecuteInTransactionAsync(async token =>
        {
            var now = await EnterBookingGatesAsync(performanceId, buyerId, token);

            if (await idempotency.FindReplayAsync(buyerId, idempotencyKey, fingerprint, token) is { } replay)
                return replay;

            var performance = await performances.GetPublishedAsync(performanceId, token)
                ?? throw new BookingRuleException(ErrorCode.NotFound, "找不到這個場次");

            if (performance.SalesStatusAt(now) != SalesStatus.OnSale)
                throw new BookingRuleException(ErrorCode.NotOnSale, "目前不在售票期間");

            var section = await performances.GetSectionAsync(performanceId, request.SectionId, token)
                ?? throw new BookingRuleException(ErrorCode.ValidationFailed, "票區不存在或不屬於這個場次");

            var policy = PurchasePolicy.For(performance.Event.Category);
            ValidateAgainstPolicy(request, policy);

            await ClearOwnExpiredHoldAsync(buyerId, performanceId, now, token);

            // 累計上限要在買家 gate 內重查：不然 A 讀到 0 張、B 買完 4 張、A 才插入，就變成 8 張
            var alreadyPaid = await orders.CountPaidSeatsAsync(buyerId, performanceId, token);
            policy.EnsureWithinLimit(alreadyPaid, request.Quantity);

            var chosen = request.SelectionMode == SelectionMode.Manual
                ? await ResolveManualAsync(performanceId, section, request.SeatIds, now, token)
                : await ResolveContiguousAsync(section, request.Quantity, now, token);

            // 先寫 hold，Seats.HoldId 的外鍵才有東西可以指
            var hold = new SeatHold(Guid.NewGuid(), buyerId, performanceId,
                                    [.. chosen.Select(seat => new SeatHoldItem(seat, section))],
                                    now, HoldDuration);
            holds.Add(hold);
            await uow.SaveChangesAsync(token);

            // ⭐ 真正的裁決在這裡：條件 UPDATE 的影響列數必須等於張數
            var seatIds = chosen.Select(seat => seat.Id).ToArray();
            var affected = await seats.TryHoldAsync(performanceId, seatIds, hold.Id,
                                                    hold.ExpiresAtUtc, now, token);
            if (affected != seatIds.Length)
                throw new BookingRuleException(ErrorCode.SeatUnavailable, "有座位剛剛被別人取走了");

            var json = PublicJson.Serialize(BookingMapper.ToDto(hold, now, orderId: null, Currency));
            idempotency.Save(buyerId, idempotencyKey, fingerprint, StatusCreated, json, now);
            await uow.SaveChangesAsync(token);

            return new IdempotencyResponse(StatusCreated, json, IsReplay: false);
        }, ct);

        // 成功的 log 放在 commit 之後：交易內的 log 只能證明「試過」，不能證明「成立了」
        if (!result.IsReplay) logger.LogInformation("HoldCreated {BuyerId} {PerformanceId}", buyerId, performanceId);

        return result;
    }

    // ── 付款 ──────────────────────────────────────────────────────────

    public async Task<IdempotencyResponse> CheckoutAsync(Guid buyerId, Guid holdId, CheckoutRequest request,
        string idempotencyKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fingerprint = IdempotencyCommand.Checkout(holdId, request);

        var response = await uow.ExecuteInTransactionAsync(async token =>
        {
            // 只是為了知道要鎖哪一場。拿到 gate 之後一定重新載入——
            // 這中間管理者的重置可能已經把它刪掉了
            var performanceId = await holds.FindPerformanceForBuyerAsync(holdId, buyerId, token)
                ?? throw NotFoundHold();

            var now = await EnterBookingGatesAsync(performanceId, buyerId, token);

            if (await idempotency.FindReplayAsync(buyerId, idempotencyKey, fingerprint, token) is { } replay)
                return replay;

            var hold = await holds.GetForBuyerAsync(holdId, buyerId, token) ?? throw NotFoundHold();

            if (hold.Status == HoldStatus.Completed)
                return await ReplayCompletedAsync(buyerId, hold, idempotencyKey, fingerprint, now, token);

            hold.EnsureCheckoutAllowed(now);

            if (request.Outcome == CheckoutOutcome.Failed)
                return await StoreMockFailureAsync(buyerId, idempotencyKey, fingerprint, now, token);

            hold.Complete(now);
            await uow.SaveChangesAsync(token);

            // 條件 UPDATE 只改「仍屬於這筆保留的 Held 座位」。少一列代表期間座位被別人拿走，
            // 整段 rollback——絕不能出現「訂單成立但座位還可賣」
            var sold = await seats.MarkSoldAsync(hold.Id, token);
            if (sold != hold.Items.Count)
                throw new BookingRuleException(ErrorCode.HoldExpired, "保留的座位已經不完整，請重新選位");

            // 既有的有效保留即使場次已暫停或停售仍可付款，所以這裡**不重跑** OnSale 檢查
            var performance = await performances.GetAsync(performanceId, token)
                ?? throw new BookingRuleException(ErrorCode.NotFound, "找不到這個場次");

            var order = Order.FromHold(Guid.NewGuid(), hold, performance, now);
            orders.Add(order);
            await uow.SaveChangesAsync(token);

            var json = PublicJson.Serialize(OrderMapper.ToDto(order));
            idempotency.Save(buyerId, idempotencyKey, fingerprint, StatusCreated, json, now);
            await uow.SaveChangesAsync(token);

            return new IdempotencyResponse(StatusCreated, json, IsReplay: false) { CreatedOrderId = order.Id };
        }, ct);

        // 成功 log 一律放在 commit 之後。
        // 背景通知（設計文件 06 第 9 節）也會掛在這個位置，階段 7 加入——
        // 它刻意在交易外面：通知掉了不影響任何票務狀態。
        if (response.CreatedOrderId is { } orderId)
            logger.LogInformation("OrderCreated {OrderId} {BuyerId}", orderId, buyerId);

        return response;
    }

    // ── 讀取 ──────────────────────────────────────────────────────────

    public async Task<HoldDto> GetHoldAsync(Guid buyerId, Guid holdId, CancellationToken ct)
    {
        // 唯讀：不開交易、不拿 gate、**也不順手把過期的整理掉**。
        // 整理過期保留是「建立新保留」那條路徑的責任（設計文件 06 第 6 節）。
        var hold = await holds.GetForBuyerAsync(holdId, buyerId, ct) ?? throw NotFoundHold();

        var orderId = hold.Status == HoldStatus.Completed
            ? (await orders.GetByHoldAsync(holdId, ct))?.Id
            : null;

        return BookingMapper.ToDto(hold, clock.GetUtcNow(), orderId, Currency);
    }

    public async Task<HoldListDto> GetActiveHoldsAsync(Guid buyerId, int performanceId, CancellationToken ct)
    {
        _ = await performances.GetPublishedAsync(performanceId, ct)
            ?? throw new BookingRuleException(ErrorCode.NotFound, "找不到這個場次");

        var now = clock.GetUtcNow();
        var hold = await holds.FindActiveAsync(buyerId, performanceId, ct);

        // 資料庫是 Active 但已經過期的，對前端來說等於沒有——它不該被導去一筆過期的保留
        return hold is null || hold.IsExpiredAt(now)
            ? new HoldListDto([])
            : new HoldListDto([BookingMapper.ToDto(hold, now, orderId: null, Currency)]);
    }

    // ── 取消 ──────────────────────────────────────────────────────────

    public Task<HoldDto> CancelHoldAsync(Guid buyerId, Guid holdId, CancellationToken ct)
        => uow.ExecuteInTransactionAsync(async token =>
        {
            var performanceId = await holds.FindPerformanceForBuyerAsync(holdId, buyerId, token)
                ?? throw NotFoundHold();

            // 取消不需要 Idempotency-Key：它的**資料效果**本身就是冪等的。
            // 但它仍然要拿同樣的兩道 gate，否則會跟付款交錯
            var now = await EnterBookingGatesAsync(performanceId, buyerId, token);

            var hold = await holds.GetForBuyerAsync(holdId, buyerId, token) ?? throw NotFoundHold();

            if (hold.Status is HoldStatus.Cancelled or HoldStatus.Expired)
                return BookingMapper.ToDto(hold, now, orderId: null, Currency);   // 重送同樣 200

            if (hold.Status == HoldStatus.Completed)
                throw new BookingRuleException(ErrorCode.HoldNotActive, "已完成的保留不能取消");

            hold.Cancel(now);
            await uow.SaveChangesAsync(token);           // 先把狀態寫進去，再放座位

            // 影響 0 列是合法的：過期的座位可能已經被別人拿走，那就不該被我們放掉
            await seats.ReleaseAsync(hold.Id, token);

            return BookingMapper.ToDto(hold, now, orderId: null, Currency);
        }, ct);

    // ── 內部 ──────────────────────────────────────────────────────────

    private const int StatusOk = 200;
    private const int StatusCreated = 201;
    private const int StatusPaymentRequired = 402;

    /// <summary>
    /// 固定順序：**場次共享 → 買家更新 → 取時間**。
    ///
    /// 順序不能換：所有購票交易都先鎖場次再鎖買家，就不會出現「A 拿著場次等買家、
    /// B 拿著買家等場次」的循環等待。時間要在兩道 gate 都拿到之後才取，
    /// 它代表的是「系統接受這次操作的時點」，不是「請求到達的時點」。
    /// </summary>
    private async Task<DateTimeOffset> EnterBookingGatesAsync(int performanceId, Guid buyerId,
                                                              CancellationToken ct)
    {
        if (!await gate.EnterPerformanceReadAsync(performanceId, ct))
            throw new BookingRuleException(ErrorCode.NotFound, "找不到這個場次");

        if (!await gate.EnterBuyerAsync(buyerId, ct))
            throw new BookingRuleException(ErrorCode.Unauthorized, "請重新登入");

        return clock.GetUtcNow();
    }

    /// <summary>
    /// 同一場次一個人只能有一筆 Active（filtered unique index）。
    /// 過期的那筆**不會自動退出索引**，所以要在這裡先把它轉成 Expired 並
    /// <c>SaveChanges</c>，否則接下來的 INSERT 會撞索引——
    /// 而且不能寄望 EF 在同一次 SaveChanges 裡先 UPDATE 再 INSERT。
    /// </summary>
    private async Task ClearOwnExpiredHoldAsync(Guid buyerId, int performanceId, DateTimeOffset now,
                                                CancellationToken ct)
    {
        var existing = await holds.FindActiveAsync(buyerId, performanceId, ct);
        if (existing is null) return;

        if (!existing.IsExpiredAt(now))
        {
            throw new BookingRuleException(ErrorCode.ActiveHoldExists, "你在這個場次已經有一筆有效的保留")
            {
                Extensions = new Dictionary<string, object?> { ["holdId"] = existing.Id }
            };
        }

        existing.Cancel(now);                       // Active ＋ 已過期 → Expired
        await uow.SaveChangesAsync(ct);
        await seats.ReleaseAsync(existing.Id, ct);
    }

    private async Task<IReadOnlyList<Seat>> ResolveManualAsync(int performanceId, Section section,
        IReadOnlyList<int> seatIds, DateTimeOffset now, CancellationToken ct)
    {
        var found = await seats.GetManyAsync(performanceId, seatIds, ct);

        if (found.Count != seatIds.Count)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "有座位不存在或不屬於這個場次");

        if (found.Any(seat => seat.SectionId != section.Id))
            throw new BookingRuleException(ErrorCode.ValidationFailed, "選到的座位不在同一個票區");

        // 早一步回饋而已——真正決定歸屬的是 TryHoldAsync 的 WHERE
        if (found.Any(seat => !seat.IsAvailableAt(now)))
            throw new BookingRuleException(ErrorCode.SeatUnavailable, "有座位已經被取走");

        return found;
    }

    private async Task<IReadOnlyList<Seat>> ResolveContiguousAsync(Section section, int quantity,
        DateTimeOffset now, CancellationToken ct)
    {
        var available = await seats.GetAvailableInSectionAsync(section.Id, now, ct);

        // 找不到就是找不到，不換區也不拆單（設計文件 02 第 4 節）
        return ContiguousSeatFinder.Find(available, quantity)
            ?? throw new BookingRuleException(ErrorCode.NoContiguousSeats,
                                              $"這個票區找不到 {quantity} 個連續座位");
    }

    private async Task<IdempotencyResponse> ReplayCompletedAsync(Guid buyerId, SeatHold hold, string key,
        byte[] fingerprint, DateTimeOffset now, CancellationToken ct)
    {
        // Completed 卻找不到訂單是資料不變量失敗，不是「正常的成功」。
        // 對 null 建 DTO 或當成成功回覆，等於把一個嚴重問題藏起來
        var order = await orders.GetByHoldAsync(hold.Id, ct)
            ?? throw new InvalidOperationException($"保留 {hold.Id} 是 Completed 但找不到對應訂單");

        var json = PublicJson.Serialize(OrderMapper.ToDto(order));
        idempotency.Save(buyerId, key, fingerprint, StatusOk, json, now);
        await uow.SaveChangesAsync(ct);

        return new IdempotencyResponse(StatusOk, json, IsReplay: false);
    }

    private async Task<IdempotencyResponse> StoreMockFailureAsync(Guid buyerId, string key, byte[] fingerprint,
        DateTimeOffset now, CancellationToken ct)
    {
        // 模擬失敗**不改任何東西**：保留還在、座位還在、到期時間不變，可以用新的 key 再試一次
        var json = PublicJson.Serialize(new StoredFailure(ErrorCode.MockPaymentFailed, "模擬付款失敗"));
        idempotency.Save(buyerId, key, fingerprint, StatusPaymentRequired, json, now);
        await uow.SaveChangesAsync(ct);

        return new IdempotencyResponse(StatusPaymentRequired, json, IsReplay: false);
    }

    /// <summary>只跟形狀有關的檢查，不需要知道是哪種活動——所以可以在交易外面做。</summary>
    private static void ValidateShape(CreateHoldRequest request)
    {
        if (!Enum.IsDefined(request.SelectionMode))
            throw new BookingRuleException(ErrorCode.ValidationFailed, "不支援的選位方式");

        if (request.SelectionMode == SelectionMode.Manual)
        {
            if (request.SeatIds.Count != request.Quantity)
                throw new BookingRuleException(ErrorCode.ValidationFailed, "選取的座位數與張數不符");

            // ⚠️ 一定要在指紋排序**之前**檢查重複，否則 [1,1] 與 [1] 會得到同一個指紋
            if (request.SeatIds.Distinct().Count() != request.SeatIds.Count)
                throw new BookingRuleException(ErrorCode.ValidationFailed, "座位不可重複");
        }
        else if (request.SeatIds.Count != 0)
        {
            throw new BookingRuleException(ErrorCode.ValidationFailed, "自動連號不接受指定座位");
        }
    }

    /// <summary>需要知道活動類別的檢查。</summary>
    private static void ValidateAgainstPolicy(CreateHoldRequest request, PurchasePolicy policy)
    {
        if (request.Quantity > policy.MaxTicketsPerBuyer)
            throw new BookingRuleException(ErrorCode.ValidationFailed,
                                           $"這個活動單次最多 {policy.MaxTicketsPerBuyer} 張");

        if (request.SelectionMode == SelectionMode.Contiguous && !policy.AllowsContiguousAllocation)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "這個活動不提供自動連號配位，請自行選位");
    }

    private static BookingRuleException NotFoundHold()
        // 別人的保留也走這裡：回 404 而不是 403，不告訴你它存在
        => new(ErrorCode.NotFound, "找不到這筆保留");
}
