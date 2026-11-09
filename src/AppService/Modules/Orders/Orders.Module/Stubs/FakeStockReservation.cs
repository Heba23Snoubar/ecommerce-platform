using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Module.Ports;

namespace Orders.Module.Stubs;

internal class FakeStockReservation : IStockReservation
{
    private const int StubAvailable = 100;
    private readonly ILogger<FakeStockReservation> _log;
    private readonly string _mode;

    public FakeStockReservation(ILogger<FakeStockReservation> log, IConfiguration config)
    {
        _log = log;
        _mode = config["Orders:FakeWarehouseMode"] ?? "ok";   // ok | unavailable | timeout
    }

    public Task<ReserveResult> ReserveAsync(Guid reservationId,
        IReadOnlyList<(Guid ProductId, int Quantity)> items, CancellationToken ct)
    {
        if (_mode == "unavailable")
            throw new WarehouseUnavailableException("Simulated: the warehouse is down.");
        if (_mode == "timeout")
            throw new WarehouseTimeoutException("Simulated: the warehouse did not respond in time.");

        var lacking = items.Where(i => i.Quantity > StubAvailable)
            .Select(i => new { productId = i.ProductId, requested = i.Quantity, available = StubAvailable })
            .ToList();

        if (lacking.Count > 0)
            return Task.FromResult(new ReserveResult(ReserveOutcome.InsufficientStock,
                "Insufficient stock for one or more products.", new { shortages = lacking }));

        _log.LogInformation("STUB reserve {ReservationId}", reservationId);
        return Task.FromResult(new ReserveResult(ReserveOutcome.Reserved));
    }

    public Task ReleaseAsync(Guid reservationId, CancellationToken ct)
    {
        _log.LogInformation("STUB release {ReservationId}", reservationId);
        return Task.CompletedTask;
    }
}