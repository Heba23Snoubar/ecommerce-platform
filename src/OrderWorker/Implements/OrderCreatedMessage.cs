namespace OrderWorker;

public sealed record OrderCreatedMessage(
    Guid MessageId,
    Guid OrderId,
    Guid UserId,
    string CustomerName,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderCreatedItemMessage> Items,
    DateTime CreatedAtUtc);

public sealed record OrderCreatedItemMessage(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);