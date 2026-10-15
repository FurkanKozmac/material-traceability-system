using System.Threading.Channels;
using backend.Common.Events;
using backend.Services.Interfaces;

namespace backend.Services.Implementations.Notifications;

public sealed class LowStockChannel : ILowStockChannel
{
    private readonly Channel<LowStockAlertEvent> _channel = Channel.CreateUnbounded<LowStockAlertEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask PublishAlertAsync(LowStockAlertEvent alert, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_channel.Writer.TryWrite(alert))
            throw new InvalidOperationException("Düşük stok uyarı kuyruğu kapalı.");
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<LowStockAlertEvent> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}
