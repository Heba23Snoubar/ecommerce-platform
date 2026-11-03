namespace Orders.Contracts;

public interface IOrdersApi
{
    Task<OrderDto> CreateAsync(Guid userId, CreateOrderRequest request, CancellationToken ct);

    /// ترمي AppException(404) إن لم يوجد الطلب أو لم يكن للمستخدم
    Task<OrderDto> GetAsync(Guid id, Guid requesterId, bool isAdmin, CancellationToken ct);

    Task<OrderPageDto> ListAsync(Guid requesterId, bool isAdmin, OrderStatus? status,
                                 int page, int pageSize, CancellationToken ct);

    Task<OrderDto> UpdateStatusAsync(Guid id, OrderStatus newStatus, CancellationToken ct);
}