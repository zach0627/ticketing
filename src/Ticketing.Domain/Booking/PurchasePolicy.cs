using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Booking;

/// <summary>
/// 每人上限與能不能連號配位，依活動類別而不同——「同一個問題、不同對象答案不同」，
/// 這是真正用得上繼承的地方（設計文件 04 第 2.2 節）。
/// 上限**不存資料表**，避免兩個真相。
/// </summary>
public abstract class PurchasePolicy
{
    public abstract int MaxTicketsPerBuyer { get; }
    public abstract bool AllowsContiguousAllocation { get; }

    public static PurchasePolicy For(EventCategory category) => category switch
    {
        EventCategory.Concert => new ConcertPurchasePolicy(),
        EventCategory.Sport => new SportsPurchasePolicy(),
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    /// <summary>共同規則：已付款張數 ＋ 本次張數不能超過上限。</summary>
    public void EnsureWithinLimit(int alreadyPaid, int requested)
    {
        if (requested <= 0)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "張數必須大於 0");
        if (requested > MaxTicketsPerBuyer)
            throw new BookingRuleException(ErrorCode.ValidationFailed, $"單次最多 {MaxTicketsPerBuyer} 張");
        if (alreadyPaid + requested > MaxTicketsPerBuyer)
            throw new BookingRuleException(ErrorCode.LimitExceeded,
                $"每人每場最多 {MaxTicketsPerBuyer} 張，你已購買 {alreadyPaid} 張");
    }
}
