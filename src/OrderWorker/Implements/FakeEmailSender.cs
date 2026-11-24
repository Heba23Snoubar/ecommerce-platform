using OrderWorker.Interface;
using StackExchange.Redis;

namespace OrderWorker.Implements;


public sealed class FakeEmailSender : IEmailSender, IDisposable
{
    private readonly ILogger<FakeEmailSender> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly int _delayMs;
    private readonly string _failPrefix;

    public FakeEmailSender(RedisSettings redisSettings, ILogger<FakeEmailSender> logger)
    {
        _logger = logger;
        _redis = ConnectionMultiplexer.Connect(redisSettings.BuildConnectionString());
        _delayMs = int.TryParse(Environment.GetEnvironmentVariable("FAKE_EMAIL_DELAY_MS"), out var d) && d > 0 ? d : 0;
        _failPrefix = Environment.GetEnvironmentVariable("FAKE_EMAIL_FAIL_PREFIX") ?? "fail-";
    }

    public async Task SendOrderConfirmationAsync(
        OrderCreatedMessage order,
        CancellationToken cancellationToken = default)
    {
        if (_delayMs > 0)
        {
            await Task.Delay(_delayMs, cancellationToken);
        }

        if (!string.IsNullOrEmpty(_failPrefix) &&
            order.CustomerEmail.StartsWith(_failPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Simulated SMTP failure for {order.CustomerEmail}");
        }

        var count = await _redis.GetDatabase().StringIncrementAsync($"emailsent:{order.OrderId}");

        _logger.LogInformation(
            "FAKE-EMAIL-SENT order={OrderId} to={Recipient} count={Count} worker={Worker}",
            order.OrderId,
            order.CustomerEmail,
            count,
            Environment.MachineName);
    }

    public void Dispose()
    {
        _redis.Dispose();
    }
}