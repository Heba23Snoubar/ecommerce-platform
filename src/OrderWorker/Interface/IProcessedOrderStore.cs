using OrderWorker;
namespace OrderWorker.Interface;

public interface IProcessedOrderStore
{
    Task<bool> TryAcquireAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<bool> HasBeenProcessedAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default);
}