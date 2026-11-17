using OrderWorker.Implements;

namespace OrderWorker.Interface;

public interface IEmailSender
{
    Task SendOrderConfirmationAsync(
        OrderCreatedMessage order,
        CancellationToken cancellationToken = default);
}