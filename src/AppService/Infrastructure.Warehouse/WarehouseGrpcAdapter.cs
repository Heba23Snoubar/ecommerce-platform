using Contracts.Warehouse;

namespace Infrastructure.Warehouse;

public class WarehouseGrpcAdapter : IWarehouseService
{
    private readonly WarehouseService.WarehouseServiceClient _client;

    public WarehouseGrpcAdapter(WarehouseService.WarehouseServiceClient client)
    {
        _client = client;
    }

    public async Task<int> GetStockAsync(string productId, CancellationToken ct = default)
    {
        var response = await _client.GetStockAsync(new GetStockRequest
        {
            ProductId = productId
        }, cancellationToken: ct);

        return response.Available;
    }

    public async Task<bool> SetStockAsync(string productId, int quantity, CancellationToken ct = default)
    {
        var response = await _client.SetStockAsync(new SetStockRequest
        {
            ProductId = productId,
            Quantity = quantity
        }, cancellationToken: ct);

        return response.Ok;
    }

    public async Task<bool> ReserveStockAsync(string reservationId, Dictionary<string, int> items, CancellationToken ct = default)
    {
        var request = new ReserveRequest
        {
            ReservationId = reservationId
        };

        foreach (var (productId, quantity) in items)
        {
            request.Items.Add(new ReserveItem
            {
                ProductId = productId,
                Quantity = quantity
            });
        }

        var response = await _client.ReserveAsync(request, cancellationToken: ct);
        return response.Status == ReserveStatus.Reserved;
    }

    public async Task<bool> ReleaseStockAsync(string reservationId, CancellationToken ct = default)
    {
        var response = await _client.ReleaseAsync(new ReleaseRequest
        {
            ReservationId = reservationId
        }, cancellationToken: ct);

        return response.Released;
    }
}