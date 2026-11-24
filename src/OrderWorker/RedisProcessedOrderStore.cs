using OrderWorker.Interface;
using StackExchange.Redis;

namespace OrderWorker;

public sealed class RedisProcessedOrderStore : IProcessedOrderStore, IDisposable
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisSettings _settings;

    public RedisProcessedOrderStore(RedisSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _connectionMultiplexer = ConnectionMultiplexer.Connect(_settings.BuildConnectionString());
    }

    private RedisKey BuildKey(Guid orderId) => $"{_settings.KeyPrefix}{orderId}";

    public async Task<bool> TryAcquireAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _connectionMultiplexer.GetDatabase();
        var key = BuildKey(orderId);
        var value = messageId.ToString();

        return await db.StringSetAsync(key, value, _settings.Ttl, When.NotExists);
    }

    public async Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _connectionMultiplexer.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(orderId));
    }

    public async Task<bool> HasBeenProcessedAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _connectionMultiplexer.GetDatabase();
        return await db.KeyExistsAsync(BuildKey(orderId));
    }

    public async Task MarkAsProcessedAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _connectionMultiplexer.GetDatabase();
        var key = BuildKey(orderId);
        var value = messageId.ToString();

        await db.StringSetAsync(key, value, _settings.Ttl, When.NotExists);
    }

    public void Dispose()
    {
        _connectionMultiplexer.Dispose();
    }
}
