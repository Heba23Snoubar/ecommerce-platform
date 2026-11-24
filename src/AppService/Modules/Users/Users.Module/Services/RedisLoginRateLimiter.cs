using System.Net;
using StackExchange.Redis;

namespace Users.Module.Services;

public sealed class RedisLoginRateLimiter : ILoginRateLimiter
{
    private const string LuaScript = """
        local current = redis.call('INCR', KEYS[1])

        if current == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end

        local ttl = redis.call('PTTL', KEYS[1])

        return { current, ttl }
        """;

    private const int PermitLimit = 5;

    private static readonly TimeSpan Window =
        TimeSpan.FromMinutes(1);

    private readonly IDatabase _database;

    public RedisLoginRateLimiter(
        IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<RateLimitResult> CheckAsync(
        string ipAddress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedIp = NormalizeIpAddress(ipAddress);

        var key = $"ratelimit:login:{normalizedIp}";

        var result = await _database.ScriptEvaluateAsync(
            LuaScript,
            new RedisKey[] { key },
            new RedisValue[]
            {
                (long)Window.TotalMilliseconds
            });

        var values = (RedisResult[]?)result;

        if (values is null || values.Length != 2)
        {
            throw new InvalidOperationException(
                "Unexpected response from Redis rate limiter script.");
        }

        var currentCount = (long)values[0];
        var ttlMilliseconds = (long)values[1];

        if (currentCount < 1 || ttlMilliseconds <= 0)
        {
            throw new InvalidOperationException(
                "Invalid response values from Redis rate limiter script.");
        }

        // Allow requests within the configured limit.
        if (currentCount <= PermitLimit)
        {
            return new RateLimitResult(
                IsAllowed: true,
                RetryAfterSeconds: 0);
        }

        // Reject requests exceeding the limit.
        var retryAfterSeconds = (int)Math.Ceiling(
            ttlMilliseconds / 1000.0);

        return new RateLimitResult(
            IsAllowed: false,
            RetryAfterSeconds: Math.Max(1, retryAfterSeconds));
    }

    private static string NormalizeIpAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var address))
        {
            throw new ArgumentException(
                "Invalid IP address.",
                nameof(ipAddress));
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily ==
            System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();

            // Aggregate IPv6 addresses by /64.
            Array.Clear(bytes, 8, 8);

            address = new IPAddress(bytes);
        }

        return address.ToString();
    }
}