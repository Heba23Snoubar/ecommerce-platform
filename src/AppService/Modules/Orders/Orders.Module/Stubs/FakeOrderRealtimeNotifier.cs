using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Contracts;
using Orders.Module.Ports;

namespace Orders.Module.Stubs;

internal class FakeOrderRealtimeNotifier : IOrderRealtimeNotifier
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ILogger<FakeOrderRealtimeNotifier> _log;
    private readonly string _mode;

    public FakeOrderRealtimeNotifier(ILogger<FakeOrderRealtimeNotifier> log, IConfiguration config)
    {
        _log = log;
        _mode = config["Orders:FakeRealtimeMode"] ?? "ok"; // ok | unavailable
    }

    public Task NotifyOrderCreatedAsync(OrderSummaryDto order, CancellationToken ct)
    {
        if (_mode == "unavailable")
            throw new InvalidOperationException("Simulated: Realtime WebSocket server is unavailable.");

        _log.LogInformation("[FAKE] order.created would be broadcast to private-admin-orders: {Payload}",
            JsonSerializer.Serialize(order, Json));
        return Task.CompletedTask;
    }

    public Task NotifyOrderStatusChangedAsync(OrderStatusChangedRealtimePayload payload, CancellationToken ct)
    {
        if (_mode == "unavailable")
            throw new InvalidOperationException("Simulated: Realtime WebSocket server is unavailable.");

        _log.LogInformation("[FAKE] order.status-changed would be broadcast to private-order.{OrderId}: {Payload}",
            payload.OrderId, JsonSerializer.Serialize(payload, Json));
        return Task.CompletedTask;
    }
}