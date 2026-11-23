using System.Text;
using System.Text.Json;
using OrderWorker.Implements;
using OrderWorker.Interface;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderWorker;

public sealed class Worker : BackgroundService
{
    public const string MainExchange = "orders.events";
    public const string MainRoutingKey = "order.created";
    public const string MainQueue = "orders.email";

    public const string DeadLetterExchange = "orders.dlx";
    public const string DeadLetterQueue = "orders.email.dlq";

    private const int MaxRetryAttempts = 3;

    private static readonly TimeSpan BaseRetryDelay =
        TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<Worker> _logger;
    private readonly RabbitMqSettings _rabbitSettings;
    private readonly SmtpSettings _smtpSettings;
    private readonly IEmailSender _emailSender;
    private readonly IProcessedOrderStore _processedOrderStore;

    private IConnection? _connection;
    private IModel? _channel;

    public Worker(
        ILogger<Worker> logger,
        RabbitMqSettings rabbitSettings,
        SmtpSettings smtpSettings,
        IEmailSender emailSender,
        IProcessedOrderStore processedOrderStore)
    {
        _logger = logger;
        _rabbitSettings = rabbitSettings;
        _smtpSettings = smtpSettings;
        _emailSender = emailSender;
        _processedOrderStore = processedOrderStore;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OrderWorker starting. Target RabbitMQ: {Host}:{Port} (User: {User})",
            _rabbitSettings.Host,
            _rabbitSettings.Port,
            _rabbitSettings.User);

        var factory = new ConnectionFactory
        {
            HostName = _rabbitSettings.Host,
            Port = _rabbitSettings.Port,
            UserName = _rabbitSettings.User,
            Password = _rabbitSettings.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        while (!stoppingToken.IsCancellationRequested &&
               (_connection is null || !_connection.IsOpen))
        {
            try
            {
                var connection =
                    factory.CreateConnection("order-worker-connection");

                connection.ConnectionShutdown += OnConnectionShutdown;

                if (connection is IAutorecoveringConnection
                    recoveringConnection)
                {
                    recoveringConnection.RecoverySucceeded +=
                        OnRecoverySucceeded;
                }

                var channel = connection.CreateModel();

                DeclareTopology(channel);

                channel.BasicQos(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += OnMessageReceivedAsync;

                channel.BasicConsume(
                    queue: MainQueue,
                    autoAck: false,
                    consumer: consumer);

                _connection = connection;
                _channel = channel;

                _logger.LogInformation(
                    "Connected to RabbitMQ and consuming queue " +
                    "'{Queue}' (prefetch=1, autoAck=false).",
                    MainQueue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not connect to RabbitMQ at {Host}:{Port}. " +
                    "Retrying in 5 seconds...",
                    _rabbitSettings.Host,
                    _rabbitSettings.Port);

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }

    private async Task OnMessageReceivedAsync(
        object sender,
        BasicDeliverEventArgs args)
    {
        var channel =
            ((AsyncEventingBasicConsumer)sender).Model;

        var rawJson =
            Encoding.UTF8.GetString(args.Body.ToArray());

        _logger.LogInformation(
            "Received message from '{Queue}' " +
            "[DeliveryTag={DeliveryTag}, Redelivered={Redelivered}].",
            MainQueue,
            args.DeliveryTag,
            args.Redelivered);

        OrderCreatedMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<OrderCreatedMessage>(
                rawJson,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Invalid JSON in message " +
                "[DeliveryTag={DeliveryTag}]. " +
                "Rejecting to DLQ without requeue. Payload: {Payload}",
                args.DeliveryTag,
                rawJson);

            channel.BasicReject(
                deliveryTag: args.DeliveryTag,
                requeue: false);

            return;
        }

        if (message is null || message.OrderId == Guid.Empty)
        {
            _logger.LogError(
                "Deserialized message is null or missing OrderId " +
                "[DeliveryTag={DeliveryTag}]. Rejecting to DLQ.",
                args.DeliveryTag);

            channel.BasicReject(
                deliveryTag: args.DeliveryTag,
                requeue: false);

            return;
        }

        try
        {
            // Atomic Redis idempotency claim:
            // SET sent:{orderId} {messageId} NX EX <ttl>
            var claimed =
                await _processedOrderStore.TryClaimAsync(
                    message.OrderId,
                    message.MessageId);

            if (!claimed)
            {
                _logger.LogWarning(
                    "Duplicate message detected for OrderId={OrderId} " +
                    "(MessageId={MessageId}, DeliveryTag={DeliveryTag}). " +
                    "Skipping email send and ACKing.",
                    message.OrderId,
                    message.MessageId,
                    args.DeliveryTag);

                channel.BasicAck(
                    deliveryTag: args.DeliveryTag,
                    multiple: false);

                return;
            }

            await ProcessOrderMessageAsync(message);

            channel.BasicAck(
                deliveryTag: args.DeliveryTag,
                multiple: false);

            _logger.LogInformation(
                "Successfully processed and ACKed message: " +
                "MessageId={MessageId}, OrderId={OrderId}, " +
                "CustomerEmail={Email}, DeliveryTag={DeliveryTag}.",
                message.MessageId,
                message.OrderId,
                message.CustomerEmail,
                args.DeliveryTag);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "All {MaxAttempts} retry attempts exhausted for " +
                "MessageId={MessageId}, OrderId={OrderId}, " +
                "DeliveryTag={DeliveryTag}. Rejecting message to DLQ " +
                "'{Dlq}' (requeue=false).",
                MaxRetryAttempts,
                message.MessageId,
                message.OrderId,
                args.DeliveryTag,
                DeadLetterQueue);

            channel.BasicReject(
                deliveryTag: args.DeliveryTag,
                requeue: false);
        }
    }

    private async Task ProcessOrderMessageAsync(
        OrderCreatedMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing order.created event: " +
            "MessageId={MessageId}, OrderId={OrderId}, " +
            "Customer={CustomerName} <{CustomerEmail}>, " +
            "ItemsCount={ItemsCount}, Total={Total} " +
            "(CreatedAtUtc={CreatedAtUtc:O})",
            message.MessageId,
            message.OrderId,
            message.CustomerName,
            message.CustomerEmail,
            message.Items?.Count ?? 0,
            message.Total,
            message.CreatedAtUtc);

        await ExecuteWithRetryAsync(
            operation: ct =>
                _emailSender.SendOrderConfirmationAsync(
                    message,
                    ct),
            messageId: message.MessageId,
            orderId: message.OrderId,
            cancellationToken: cancellationToken);
    }

    private async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        Guid messageId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1;
             attempt <= MaxRetryAttempts;
             attempt++)
        {
            try
            {
                await operation(cancellationToken);
                return;
            }
            catch (Exception ex)
                when (attempt < MaxRetryAttempts &&
                      !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(
                    BaseRetryDelay.TotalSeconds *
                    Math.Pow(2, attempt - 1));

                _logger.LogWarning(
                    ex,
                    "Attempt {Attempt}/{MaxAttempts} failed for " +
                    "MessageId={MessageId}, OrderId={OrderId}. " +
                    "Retrying in {DelaySeconds}s...",
                    attempt,
                    MaxRetryAttempts,
                    messageId,
                    orderId,
                    delay.TotalSeconds);

                await Task.Delay(
                    delay,
                    cancellationToken);
            }
        }
    }

    private void OnConnectionShutdown(
        object? sender,
        ShutdownEventArgs args)
    {
        _logger.LogWarning(
            "RabbitMQ connection lost ({ReplyText}). " +
            "Automatic recovery is active...",
            args.ReplyText);
    }

    private void OnRecoverySucceeded(
        object? sender,
        EventArgs args)
    {
        _logger.LogInformation(
            "RabbitMQ connection recovered automatically.");
    }

    private static void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(
            exchange: DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.QueueBind(
            queue: DeadLetterQueue,
            exchange: DeadLetterExchange,
            routingKey: DeadLetterQueue);

        channel.ExchangeDeclare(
            exchange: MainExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        var mainQueueArguments = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterQueue
        };

        channel.QueueDeclare(
            queue: MainQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments);

        channel.QueueBind(
            queue: MainQueue,
            exchange: MainExchange,
            routingKey: MainRoutingKey);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();

        _connection?.Close();
        _connection?.Dispose();

        base.Dispose();
    }
}