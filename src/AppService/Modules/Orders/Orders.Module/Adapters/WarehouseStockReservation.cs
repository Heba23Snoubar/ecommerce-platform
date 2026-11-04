using Grpc.Core;
using Infrastructure.Warehouse;
using Microsoft.Extensions.Logging;
using Orders.Module.Ports;

namespace Orders.Module.Adapters;

internal class WarehouseStockReservation : IStockReservation
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(3);

    private readonly IWarehouseService _warehouse;
    private readonly ILogger<WarehouseStockReservation> _log;

    public WarehouseStockReservation(IWarehouseService warehouse, ILogger<WarehouseStockReservation> log)
    {
        _warehouse = warehouse;
        _log = log;
    }

    public async Task<ReserveResult> ReserveAsync(Guid reservationId,
        IReadOnlyList<(Guid ProductId, int Quantity)> items, CancellationToken ct)
    {
        var request = items.ToDictionary(i => i.ProductId.ToString(), i => i.Quantity);

        var reserved = await CallAsync(
            t => _warehouse.ReserveStockAsync(reservationId.ToString(), request, t), ct);

        if (reserved)
            return new ReserveResult(ReserveOutcome.Reserved);

        // الرفض: نقرأ الكميات المتوفرة لنعطي المستخدم رسالة واضحة
        var shortages = new List<object>();
        foreach (var item in items)
        {
            try
            {
                var available = await CallAsync(t => _warehouse.GetStockAsync(item.ProductId.ToString(), t), ct);
                if (available < item.Quantity)
                    shortages.Add(new { productId = item.ProductId, requested = item.Quantity, available });
            }
            catch (Exception ex) when (ex is WarehouseUnavailableException or WarehouseTimeoutException)
            {
                _log.LogWarning(ex, "Could not read stock details after a rejected reservation");
                break;
            }
        }

        return new ReserveResult(ReserveOutcome.InsufficientStock,
            "Insufficient stock for one or more products.",
            new { shortages });
    }

    public async Task ReleaseAsync(Guid reservationId, CancellationToken ct)
    {
        await CallAsync(t => _warehouse.ReleaseStockAsync(reservationId.ToString(), t), ct);
    }

    private static async Task<T> CallAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CallTimeout);

        try
        {
            return await call(timeout.Token);
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            throw; // المستخدم هو من ألغى الطلب
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            throw new WarehouseUnavailableException("The warehouse service is unavailable.", ex);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded
            || (ex.StatusCode == StatusCode.Cancelled && timeout.IsCancellationRequested))
        {
            throw new WarehouseTimeoutException("The warehouse service did not respond in time.", ex);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new WarehouseTimeoutException("The warehouse service did not respond in time.");
        }
        catch (HttpRequestException ex)
        {
            throw new WarehouseUnavailableException("The warehouse service is unavailable.", ex);
        }
        catch (RpcException ex)
        {
            throw new WarehouseUnavailableException($"The warehouse call failed ({ex.StatusCode}).", ex);
        }
    }
}