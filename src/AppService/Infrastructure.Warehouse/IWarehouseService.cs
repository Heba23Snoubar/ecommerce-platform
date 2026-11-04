namespace Infrastructure.Warehouse;

public interface IWarehouseService
{
    Task<int> GetStockAsync(string productId, CancellationToken ct = default);
    Task<bool> SetStockAsync(string productId, int quantity, CancellationToken ct = default);
    Task<bool> ReserveStockAsync(string reservationId, Dictionary<string, int> items, CancellationToken ct = default);
    Task<bool> ReleaseStockAsync(string reservationId, CancellationToken ct = default);
}