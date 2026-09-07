namespace Ticketing.Domain.Booking;

/// <summary>演唱會：每人 4 張，必須手選（不提供連號配位）。</summary>
public sealed class ConcertPurchasePolicy : PurchasePolicy
{
    public override int MaxTicketsPerBuyer => 4;
    public override bool AllowsContiguousAllocation => false;
}
