using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Module.Ports;
using RabbitMQ.Client;

namespace Orders.Module.Messaging;

internal sealed class RabbitMqOrderEventPublisher : IOrderEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(3);

    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqOrderEventPublisher> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqOrderEventPublisher(IConfiguration config, ILogger<RabbitMqOrderEventPublisher> log)
    {
        _factory = RabbitMqConfig.CreateFactory(config);
        _log = log;
    }

    public async Task PublishOrderCreatedAsync(OrderCreatedMessage message, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(PublishTimeout);

        await _gate.WaitAsync(cts.Token);
        try
        {
            var channel = await EnsureChannelAsync(cts.Token);

            var body = JsonSerializer.SerializeToUtf8Bytes(message, Json);
            var props = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.MessageId.ToString()
            };

            await channel.BasicPublishAsync(
                exchange: OrdersTopology.Exchange,
                routingKey: OrdersTopology.RoutingKeyCreated,
                mandatory: true,
                basicProperties: props,
                body: body,
                cancellationToken: cts.Token);

            _log.LogInformation("Published order.created for order {OrderId} (message {MessageId})",
                message.OrderId, message.MessageId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            return _channel;

        await CloseAsync();

        _connection = await _factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            ct);

        
        await OrdersTopology.DeclareAsync(_channel, ct);
        return _channel;
    }

    private async Task CloseAsync()
    {
        try { if (_channel is not null) await _channel.DisposeAsync(); }
        catch (Exception) { /* القناة معطوبة أصلاً */ }
        try { if (_connection is not null) await _connection.DisposeAsync(); }
        catch (Exception) { /* الاتصال معطوب أصلاً */ }
        _channel = null;
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _gate.Dispose();
    }
}