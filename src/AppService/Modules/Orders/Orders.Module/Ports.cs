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