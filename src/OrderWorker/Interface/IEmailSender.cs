namespace OrderWorker;

public interface IEmailSender
{
    Task SendOrderConfirmationAsync(
        OrderCreatedMessage order,
        CancellationToken cancellationToken = default);
}