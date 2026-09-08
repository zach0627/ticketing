using System.ComponentModel.DataAnnotations;

namespace Ticketing.Application.Admin.Dtos;

/// <summary>
/// 重置會刪掉所有購買資料，所以要打字確認。
///
/// <c>RegularExpression</c> 讓**API 端**精確比對 `RESET`——
/// 只靠前端的確認框防呆是不夠的，任何人都能直接打 API（設計文件 13 第 2 節）。
/// </summary>
public sealed record ResetRequest
{
    [Required, RegularExpression("^RESET$")]
    public string Confirmation { get; init; } = "";
}

/// <summary>
/// <paramref name="EarliestPerformanceUtc"/> 是**平移之後**的最早開演時間——
/// 管理者關心的是「現在最早那一場是什麼時候」，不是重置前的舊值。
/// </summary>
public sealed record ResetResult(
    int OrdersDeleted,
    int HoldsDeleted,
    int SeatsReleased,
    DateTimeOffset EarliestPerformanceUtc);
