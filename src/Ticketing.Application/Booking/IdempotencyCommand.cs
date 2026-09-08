using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ticketing.Application.Booking.Dtos;

namespace Ticketing.Application.Booking;

/// <summary>
/// 把「這個請求到底想做什麼」壓成一段固定的字串，再取 SHA-256。
///
/// **不能只 hash body**（設計文件 06 第 7 節）。同一個人拿相同 key 與
/// <c>{outcome:"Succeeded"}</c> 去付另一筆保留，body 完全一樣——
/// 只 hash body 的話就會把第一張訂單當成第二張回給他。
/// 所以指紋包含：版本、HTTP 方法、路由範本、**目標資源 ID**、正規化過的 body。
///
/// 「正規化」的重點是**同樣的意思一定得到同樣的字串**：
/// 屬性名排序固定、enum 用字串、Guid 用小寫 D 格式、座位 ID 排序過
/// （所以 <c>[2,1]</c> 與 <c>[1,2]</c> 是同一個請求）。
/// 排序**之前**必須先驗過沒有重複，否則 <c>[1,1]</c> 與 <c>[1]</c> 會撞在一起。
/// </summary>
public static class IdempotencyCommand
{
    /// <summary>指紋的版本。改了正規化規則就要加，讓舊紀錄自然失效而不是被誤判成相同。</summary>
    private const int Version = 1;

    public static byte[] CreateHold(int performanceId, CreateHoldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Fingerprint(
            method: "POST",
            route: "/performances/{id}/holds",
            target: performanceId.ToString(CultureInfo.InvariantCulture),
            body: new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["quantity"] = request.Quantity,
                ["sectionId"] = request.SectionId,
                ["seatIds"] = request.SeatIds.Order().ToArray(),
                ["selectionMode"] = request.SelectionMode.ToString()
            });
    }

    public static byte[] Checkout(Guid holdId, CheckoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Fingerprint(
            method: "POST",
            route: "/holds/{id}/checkout",
            target: holdId.ToString("D", CultureInfo.InvariantCulture),
            body: new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome"] = request.Outcome.ToString()
            });
    }

    private static byte[] Fingerprint(string method, string route, string target,
                                      SortedDictionary<string, object?> body)
    {
        // SortedDictionary ＋ Ordinal 比較子：序列化出來的鍵順序不受宣告順序影響
        var canonical = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["body"] = body,
            ["method"] = method,
            ["route"] = route,
            ["target"] = target,
            ["version"] = Version
        };

        var json = JsonSerializer.Serialize(canonical);
        return SHA256.HashData(Encoding.UTF8.GetBytes(json));
    }
}
