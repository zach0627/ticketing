namespace Ticketing.Domain.Common;

/// <summary>
/// 業務錯誤碼。前端依 <c>code</c> 決定顯示什麼，所以字串值是 API 契約的一部分，不可隨意更名。
/// Domain 只表達「哪一條規則不成立」，HTTP 狀態由 Api 層對照（設計文件 13 第 3 節）。
/// </summary>
public static class ErrorCode
{
    // ── Domain 與 Application 會丟出的業務碼 ──
    public const string ValidationFailed = nameof(ValidationFailed);
    public const string Unauthorized = nameof(Unauthorized);
    public const string Forbidden = nameof(Forbidden);
    public const string NotFound = nameof(NotFound);
    public const string SeatUnavailable = nameof(SeatUnavailable);
    public const string NoContiguousSeats = nameof(NoContiguousSeats);
    public const string LimitExceeded = nameof(LimitExceeded);
    public const string ActiveHoldExists = nameof(ActiveHoldExists);
    public const string HoldExpired = nameof(HoldExpired);
    public const string HoldNotActive = nameof(HoldNotActive);
    public const string NotOnSale = nameof(NotOnSale);
    public const string IdempotencyKeyReuse = nameof(IdempotencyKeyReuse);
    public const string EmailAlreadyRegistered = nameof(EmailAlreadyRegistered);
    public const string MockPaymentFailed = nameof(MockPaymentFailed);

    // ── 只由 Api 層產生（框架層級的失敗），列在這裡是為了讓對照表有單一來源 ──
    public const string MethodNotAllowed = nameof(MethodNotAllowed);
    public const string PayloadTooLarge = nameof(PayloadTooLarge);
    public const string UnsupportedMediaType = nameof(UnsupportedMediaType);
    public const string RateLimited = nameof(RateLimited);
    public const string InternalError = nameof(InternalError);
    public const string ServiceUnavailable = nameof(ServiceUnavailable);
}
