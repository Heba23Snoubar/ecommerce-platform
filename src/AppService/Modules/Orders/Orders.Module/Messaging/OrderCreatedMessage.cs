using Orders.Module.Data;

namespace Orders.Module.Messaging;

internal record OrderCreatedItem(
    Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

internal record OrderCreatedMessage(
    Guid MessageId,
    Guid OrderId,
    Guid UserId,
    string CustomerName,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderCreatedItem> Items,
    DateTime CreatedAtUtc)
{

    public static OrderCreatedMessage From(Order order, string customerName, string customerEmail) =>
        new(
            MessageId: Guid.NewGuid(),
            OrderId: order.Id,
            UserId: order.UserId,
            CustomerName: customerName,
            CustomerEmail: customerEmail,
            Total: order.Total,
            Items: order.Items
                .Select(i => new OrderCreatedItem(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                .ToList(),
            CreatedAtUtc: DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc));
}