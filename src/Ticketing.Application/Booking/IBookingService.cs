using Ticketing.Application.Booking.Dtos;

namespace Ticketing.Application.Booking;

/// <summary>
/// 保留、付款、取消。
///
/// 兩個寫入方法回 <see cref="IdempotencyResponse"/> 而不是 DTO：
/// 因為「同一個 key 重送」要拿回**上一次的完整結果**，包含當時的 HTTP 狀態
/// （設計文件 06 第 7 節）。讀取方法回一般 DTO。
///
/// 所有方法第一個參數都是 <c>buyerId</c>——每一筆查詢都帶上它，
/// 別人的資源查不到（而不是查到之後才判斷有沒有權限）。
/// </summary>
public interface IBookingService
{
    Task<IdempotencyResponse> CreateHoldAsync(Guid buyerId, int performanceId, CreateHoldRequest request,
                                              string idempotencyKey, CancellationToken ct);

    Task<IdempotencyResponse> CheckoutAsync(Guid buyerId, Guid holdId, CheckoutRequest request,
                                            string idempotencyKey, CancellationToken ct);

    Task<HoldDto> GetHoldAsync(Guid buyerId, Guid holdId, CancellationToken ct);

    /// <summary>某場次目前**還沒到期**的本人保留，0 或 1 筆。選位頁進來先問這個。</summary>
    Task<HoldListDto> GetActiveHoldsAsync(Guid buyerId, int performanceId, CancellationToken ct);

    Task<HoldDto> CancelHoldAsync(Guid buyerId, Guid holdId, CancellationToken ct);
}
