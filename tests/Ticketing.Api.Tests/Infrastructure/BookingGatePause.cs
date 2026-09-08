using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ticketing.Application.Abstractions;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 讓一個請求**停在「已經拿到買家 gate、還沒讀時間」的那一刻**。
///
/// 為什麼需要這個？併發測試如果只是「同時發兩個請求然後祈禱」，
/// 綠燈可能只是運氣好，紅燈也不知道是哪一種交錯（設計文件 10 第 1 節：
/// 用 barrier 控制交錯，不用 sleep 碰運氣）。
///
/// 有了這個開關就能精確地說：「A 先拿到 gate 並停住 → 讓 B 進來 → 放行 A」，
/// 而且反過來再跑一次。
/// </summary>
public sealed class BookingGatePause
{
    private TaskCompletionSource _parked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _armed;

    /// <summary>下一個通過買家 gate 的請求會停住。回傳的 Task 在它真的停住時完成。</summary>
    public Task ArmAsync()
    {
        _parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _armed, 1);
        return _parked.Task;
    }

    public void Release() => _release.TrySetResult();

    internal async Task WaitIfArmedAsync(CancellationToken ct)
    {
        // Exchange 保證只有一個請求會被攔下來，之後的直接通過
        if (Interlocked.Exchange(ref _armed, 0) == 0) return;

        _parked.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
    }
}

/// <summary>
/// 真的 gate 外面包一層暫停鉤子。**只換這一個介面**——
/// SQL、交易、Service 流程全部是正式的那一套。
/// </summary>
public sealed class PausingBookingWriteGate(SqlBookingWriteGate inner, BookingGatePause pause)
    : IBookingWriteGate
{
    public Task<bool> EnterPerformanceReadAsync(int performanceId, CancellationToken ct)
        => inner.EnterPerformanceReadAsync(performanceId, ct);

    public Task<bool> EnterPerformanceWriteAsync(int performanceId, CancellationToken ct)
        => inner.EnterPerformanceWriteAsync(performanceId, ct);

    public Task EnterAllPerformancesWriteAsync(CancellationToken ct)
        => inner.EnterAllPerformancesWriteAsync(ct);

    public async Task<bool> EnterBuyerAsync(Guid buyerId, CancellationToken ct)
    {
        var entered = await inner.EnterBuyerAsync(buyerId, ct);

        // 停在這裡＝這個交易**已經握著買家的 UPDLOCK**。
        // 同一個買家的第二個請求會在 SQL Server 裡排隊，直到我們放行為止。
        await pause.WaitIfArmedAsync(ct);

        return entered;
    }
}

internal static class GatePauseRegistration
{
    public static Action<IServiceCollection> Using(BookingGatePause pause)
        => services =>
        {
            services.AddSingleton(pause);
            services.AddScoped<SqlBookingWriteGate>();
            services.RemoveAll<IBookingWriteGate>();
            services.AddScoped<IBookingWriteGate, PausingBookingWriteGate>();
        };
}
