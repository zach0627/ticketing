using System.Globalization;
using Ticketing.Domain.Common;

namespace Ticketing.Api.Auth;

/// <summary>
/// <c>Idempotency-Key</c> 標頭的解析與正規化。
///
/// 為什麼要正規化？因為同一個 UUID 有好幾種寫法（大小寫、有沒有大括號、有沒有連字號）。
/// 不統一的話，同一個人重送同一個 key 卻寫成大寫，就會被當成新的請求，
/// 於是他買了第二次（設計文件 13 第 2 節）。
///
/// <c>TryParseExact("D")</c> 只接受標準的 8-4-4-4-12 格式：
/// 嚴格一點的好處是行為可預測，而且**重複的標頭**（ASP.NET 會併成 "a,b"）也會在這裡被擋下。
/// </summary>
internal static class IdempotencyKey
{
    public static string Canonical(string? raw)
    {
        if (!Guid.TryParseExact(raw?.Trim(), "D", out var key) || key == Guid.Empty)
            throw new BookingRuleException(ErrorCode.ValidationFailed,
                                           "Idempotency-Key 必須是標準格式的 UUID");

        return key.ToString("D", CultureInfo.InvariantCulture);   // 一律小寫連字號格式
    }
}
