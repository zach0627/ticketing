namespace Ticketing.Application.Admin.Dtos;

/// <summary>
/// 後台首頁。三個數字 ＋ 全部場次的狀態。
/// <c>ActiveHolds</c> 是**還沒到期**的保留數，不是資料庫裡 Status='Active' 的筆數——
/// 那兩個數字在沒有背景清理程式的設計下並不相同（設計文件 04 第 5 節）。
/// </summary>
public sealed record DashboardDto(
    int ActiveHolds,
    int SoldSeats,
    int Orders,
    DateTimeOffset ServerNowUtc,
    IReadOnlyList<AdminPerformanceDto> Performances);
