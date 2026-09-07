namespace Ticketing.Domain.Booking;

/// <summary>運動賽事：每人 6 張，可以要求同排連號配位。</summary>
public sealed class SportsPurchasePolicy : PurchasePolicy
{
    public override int MaxTicketsPerBuyer => 6;
    public override bool AllowsContiguousAllocation => true;
}
