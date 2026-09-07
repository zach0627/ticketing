namespace Ticketing.Application.Abstractions;

/// <summary>
/// 購票寫入的交易存取協定。這不是 Repository——它不取資料，只是在**目前這個交易**裡
/// 對固定的資料列取得鎖，用來把並行的寫入排出先後（設計文件 06 第 3.1 節）。
///
/// 購票固定順序：<b>場次共享 → 買家更新</b>；管理暫停／重置用場次排他。
/// 單筆方法回 <c>false</c> 表示目標列不存在，由 Service 決定錯誤碼。
/// 所有 gate 都持有到交易結束。
/// </summary>
public interface IBookingWriteGate
{
    /// <summary>購票的第一道：對場次取共享鎖，不同買家可以並行。</summary>
    Task<bool> EnterPerformanceReadAsync(int performanceId, CancellationToken ct);

    /// <summary>購票的第二道：對買家取更新鎖，把同一個人的動作序列化。</summary>
    Task<bool> EnterBuyerAsync(Guid buyerId, CancellationToken ct);

    /// <summary>暫停／恢復與單場重置：對場次取排他鎖。不先取共享再升級。</summary>
    Task<bool> EnterPerformanceWriteAsync(int performanceId, CancellationToken ct);

    /// <summary>全域重置：依 Id 升序逐筆取得所有場次的排他鎖。</summary>
    Task EnterAllPerformancesWriteAsync(CancellationToken ct);
}
