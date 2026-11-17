namespace OrderWorker.Interface;

public interface IProcessedOrderStore
{
    Task<bool> HasBeenProcessedAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default);
}