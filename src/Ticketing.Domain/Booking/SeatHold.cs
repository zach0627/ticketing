using Ticketing.Domain.Common;

namespace Ticketing.Domain.Booking;

/// <summary>
/// 5 分鐘內屬於某人的一批座位，以及它的狀態機。
///
/// 三個刻意的設計（設計文件 04 第 2.1 節）：
/// ① <c>now</c> 一律由外部傳入，測試才打得到「剛好到期」那一秒；
/// ② setter 全部 private，狀態只能經由方法改；
/// ③ <see cref="Complete"/> 只保證**這個物件**合法，
///    不代表座位已經是它的——那要靠交易裡的條件 UPDATE。
/// </summary>
public sealed class SeatHold
{
    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
    public int PerformanceId { get; private set; }
    public HoldStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public decimal TotalAmount { get; private set; }

    private readonly List<SeatHoldItem> _items = [];

    /// <summary>唯讀包裝，呼叫端無法轉型成 List 後修改。</summary>
    public IReadOnlyList<SeatHoldItem> Items => _items.AsReadOnly();

    private SeatHold() { }                                  // EF Core 用（以 backing field 對應 _items）

    public SeatHold(Guid id, Guid buyerId, int performanceId,
                    IReadOnlyList<SeatHoldItem> items, DateTimeOffset now, TimeSpan holdFor)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0
            || items.Select(i => i.SeatId).Distinct().Count() != items.Count
            || holdFor <= TimeSpan.Zero)
        {
            throw new BookingRuleException(ErrorCode.ValidationFailed, "座位清單或保留期限不合法");
        }

        Id = id;
        BuyerId = buyerId;
        PerformanceId = performanceId;
        Status = HoldStatus.Active;
        CreatedAtUtc = now;
        ExpiresAtUtc = now + holdFor;                       // 建立時就固定，之後不延長
        _items.AddRange(items);
        TotalAmount = items.Sum(i => i.UnitPrice);          // 總額由 Domain 算，不信任前端
    }

    /// <summary>剛好等於到期時間也算到期。</summary>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAtUtc;

    /// <summary>對外顯示用：資料庫還是 Active 但已過期 → 回 Expired。</summary>
    public HoldStatus StatusAt(DateTimeOffset now)
        => Status == HoldStatus.Active && IsExpiredAt(now) ? HoldStatus.Expired : Status;

    /// <summary>只檢查不改狀態：付款成功與模擬失敗都要先過這一關。</summary>
    public void EnsureCheckoutAllowed(DateTimeOffset now)
    {
        if (Status == HoldStatus.Expired)
            throw new BookingRuleException(ErrorCode.HoldExpired, "保留已到期");
        if (Status != HoldStatus.Active)
            throw new BookingRuleException(ErrorCode.HoldNotActive, "保留不是有效狀態");
        if (IsExpiredAt(now))
            throw new BookingRuleException(ErrorCode.HoldExpired, "保留已到期");
    }

    /// <summary>模擬付款成功時呼叫。</summary>
    public void Complete(DateTimeOffset now)
    {
        EnsureCheckoutAllowed(now);
        Status = HoldStatus.Completed;
    }

    /// <summary>
    /// 取消，或建立新保留時整理自己的過期保留。
    /// 已經是終態就當作沒事（冪等），已完成的不能取消。
    /// </summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status is HoldStatus.Cancelled or HoldStatus.Expired) return;
        if (Status == HoldStatus.Completed)
            throw new BookingRuleException(ErrorCode.HoldNotActive, "已完成的保留不能取消");

        Status = IsExpiredAt(now) ? HoldStatus.Expired : HoldStatus.Cancelled;
    }
}
