namespace Orders.Contracts;

public record OrderItemRequest(Guid ProductId, int Quantity);
public record CreateOrderRequest(List<OrderItemRequest> Items);
public record UpdateOrderStatusRequest(OrderStatus Status);

public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);
public record OrderDto(Guid Id, Guid UserId, OrderStatus Status, decimal Total,
                       DateTime CreatedAt, List<OrderItemDto> Items);

public record OrderSummaryDto(Guid Id, Guid UserId, OrderStatus Status,
                              decimal Total, DateTime CreatedAt, int ItemCount);

public record OrderPageDto(List<OrderSummaryDto> Items, int Page, int PageSize, int Total);