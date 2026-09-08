using System.ComponentModel.DataAnnotations;

namespace Ticketing.Application.Booking.Dtos;

/// <summary>模擬付款的結果由前端指定——這是展示用的假金流，沒有第三方支付。</summary>
public sealed record CheckoutRequest
{
    [Required]
    public CheckoutOutcome Outcome { get; init; }
}

public enum CheckoutOutcome
{
    Succeeded,
    Failed
}
