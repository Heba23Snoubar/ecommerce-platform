namespace Orders.Module.Ports;

internal record ProductInfo(Guid Id, string Name, decimal Price);

internal interface IProductCatalog
{
    Task<IReadOnlyList<ProductInfo>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
}

internal enum ReserveOutcome { Reserved, InsufficientStock, ProductNotFound }

internal record ReserveResult(ReserveOutcome Outcome, string? Message = null, object? Details = null);

internal interface IStockReservation
{
    Task<ReserveResult> ReserveAsync(Guid reservationId,
        IReadOnlyList<(Guid ProductId, int Quantity)> items, CancellationToken ct);

    Task ReleaseAsync(Guid reservationId, CancellationToken ct);
}
internal record CartLine(Guid ProductId, int Quantity);

internal interface ICartSource
{
    /// عناصر سلة المستخدم (قائمة فارغة إن لم توجد سلة أو كانت فارغة)
    Task<IReadOnlyList<CartLine>> GetItemsAsync(Guid userId, CancellationToken ct);

    Task ClearAsync(Guid userId, CancellationToken ct);
}

internal class WarehouseUnavailableException : Exception
{
    public WarehouseUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

internal class WarehouseTimeoutException : Exception
{
    public WarehouseTimeoutException(string message, Exception? inner = null) : base(message, inner) { }
}