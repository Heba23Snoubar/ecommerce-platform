using RabbitMQ.Client;

namespace Orders.Module.Messaging;

internal static class OrdersTopology
{
    public const string Exchange = "orders.events";
    public const string RoutingKeyCreated = "order.created";
    public const string EmailQueue = "orders.email";
    public const string DeadLetterExchange = "orders.dlx";
    public const string DeadLetterQueue = "orders.email.dlq";
    public const string DeadLetterRoutingKey = "orders.email.dlq";

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct,
            durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false,
            autoDelete: false, arguments: null, cancellationToken: ct);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange,
            DeadLetterRoutingKey, cancellationToken: ct);

        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct,
            durable: true, autoDelete: false, cancellationToken: ct);
        var args = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
        };
        await channel.QueueDeclareAsync(EmailQueue, durable: true, exclusive: false,
            autoDelete: false, arguments: args, cancellationToken: ct);
        await channel.QueueBindAsync(EmailQueue, Exchange, RoutingKeyCreated,
            cancellationToken: ct);
    }
}