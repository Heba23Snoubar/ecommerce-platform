namespace OrderWorker.Interface;

public interface IProcessedOrderStore
{
    Task<bool> TryClaimAsync(
        Guid orderId,
        Guid messageId,
        CancellationToken cancellationToken = default);
}