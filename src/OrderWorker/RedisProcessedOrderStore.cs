using OrderWorker.Interface;
using StackExchange.Redis;

namespace OrderWorker;

public sealed class RedisProcessedOrderStore : IProcessedOrderStore, IDisposable
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisSettings _settings;
    private readonly TimeSpan _lockLease;

    public RedisProcessedOrderStore(RedisSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _connectionMultiplexer = ConnectionMultiplexer.Connect(_settings.BuildConnectionString());
        var leaseSeconds = int.TryParse(Environment.GetEnvironmentVariable("LOCK_TTL_SECONDS"), out var s) && s > 0 ? s : 60;
        _lockLease = TimeSpan.FromSeconds(leaseSeconds);
    }

    private RedisKey SentKey(Guid orderId) => $"{_settings.KeyPrefix}{orderId}";
    private RedisKey LockKey(Guid orderId) => $"lock:{orderId}";

    public async Task<bool> TryAcquireAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _connectionMultiplexer.GetDatabase();
        return await db.StringSetAsync(LockKey(orderId), messageId.ToString(), _lockLease, When.NotExists);
    }

    public async Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _connectionMultiplexer.GetDatabase();
        await db.KeyDeleteAsync(LockKey(orderId));
    }

    public async Task<bool> HasBeenProcessedAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _connectionMultiplexer.GetDatabase();
        return await db.KeyExistsAsync(SentKey(orderId));
    }

    public async Task MarkAsProcessedAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _connectionMultiplexer.GetDatabase();
        await db.StringSetAsync(SentKey(orderId), messageId.ToString(), _settings.Ttl);
    }

    public void Dispose()
    {
        _connectionMultiplexer.Dispose();
    }
}
