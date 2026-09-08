using System.Threading.Channels;
using Ticketing.Application.Abstractions;

namespace Ticketing.Infrastructure.Messaging;

/// <summary>
/// 程序內的有界佇列。**刻意會掉訊息**（設計文件 06 第 9 節）。
///
/// 三個設計決定：
/// <list type="bullet">
/// <item><b>有界（100）</b>：無界佇列在下游卡住時會把記憶體吃光，那是比掉通知更糟的失敗。</item>
/// <item><b>只有 TryWrite</b>：排隊發生在交易 commit **之後**，不能讓它阻塞回應。
///       滿了就回 false，呼叫端記一筆 warning。</item>
/// <item><b>SingleReader</b>：只有一個 worker 在讀，Channel 可以省掉同步成本。</item>
/// </list>
///
/// API 重啟或程序中止會丟掉還沒處理的通知。這是**明確接受**的取捨：
/// 沒有任何票務狀態依賴通知成功。要保證送達就得換 Outbox（設計文件 17）。
/// </summary>
public sealed class ChannelOrderNotificationQueue : IOrderNotificationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,   // 配 TryWrite：滿了直接回 false，不等
            SingleReader = true
        });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public bool TryEnqueue(Guid orderId) => _channel.Writer.TryWrite(orderId);
}
