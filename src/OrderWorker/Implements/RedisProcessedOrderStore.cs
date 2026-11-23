using OrderWorker.Interface;
using StackExchange.Redis;

namespace OrderWorker.Implements;

public sealed class RedisProcessedOrderStore : IProcessedOrderStore
{
    private readonly IDatabase _database;
    private readonly TimeSpan _ttl;

    public RedisProcessedOrderStore(
        IConnectionMultiplexer connection,
        RedisSettings settings)
    {
        _database = connection.GetDatabase();
        _ttl = settings.IdempotencyTtl;
    }

    public async Task<bool> TryClaimAsync(
        Guid orderId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = new RedisKey($"sent:{orderId}");
        var value = new RedisValue(messageId.ToString());

        return await _database.StringSetAsync(
            key,
            value,
            _ttl,
            When.NotExists);
    }
}