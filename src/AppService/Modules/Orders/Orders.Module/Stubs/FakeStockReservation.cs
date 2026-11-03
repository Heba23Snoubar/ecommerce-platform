using Microsoft.Extensions.Logging;
using Orders.Module.Ports;

namespace Orders.Module.Stubs;

internal class FakeStockReservation : IStockReservation
{
    private const int StubAvailable = 100;
    private readonly ILogger<FakeStockReservation> _log;

    public FakeStockReservation(ILogger<FakeStockReservation> log) => _log = log;

    public Task<ReserveResult> ReserveAsync(Guid reservationId,
        IReadOnlyList<(Guid ProductId, int Quantity)> items, CancellationToken ct)
    {
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