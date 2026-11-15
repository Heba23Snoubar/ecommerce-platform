using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Module.Messaging;
using Orders.Module.Ports;

namespace Orders.Module.Stubs;

internal class FakeOrderEventPublisher : IOrderEventPublisher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ILogger<FakeOrderEventPublisher> _log;
    private readonly string _mode;

    public FakeOrderEventPublisher(ILogger<FakeOrderEventPublisher> log, IConfiguration config)
    {
        _log = log;
        _mode = config["Orders:FakeEventsMode"] ?? "ok";   // ok | unavailable
    }

    public Task PublishOrderCreatedAsync(OrderCreatedMessage message, CancellationToken ct)
    {
        if (_mode == "unavailable")
            throw new InvalidOperationException("Simulated: RabbitMQ is unavailable.");

        _log.LogInformation("[FAKE] order.created would be published: {Payload}",
            JsonSerializer.Serialize(message, Json));
        return Task.CompletedTask;
    }
}