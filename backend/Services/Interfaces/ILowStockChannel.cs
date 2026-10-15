using System.Runtime.CompilerServices;
using backend.Common.Events;

namespace backend.Services.Interfaces;

public interface ILowStockChannel
{
    ValueTask PublishAlertAsync(LowStockAlertEvent alert, CancellationToken ct = default);
    IAsyncEnumerable<LowStockAlertEvent> ReadAllAsync(CancellationToken ct = default);
}
