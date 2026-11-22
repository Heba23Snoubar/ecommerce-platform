using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shared.Database;
using StackExchange.Redis;

namespace Catalog.Module.Infrastructure.Cache
{
    public sealed class RedisWriteRoutingMarkerStore : IWriteRoutingMarkerStore
    {
        private const string KeyPrefix = "app:v1:db-routing:recent-write";
        private static readonly TimeSpan MarkerTtl = TimeSpan.FromSeconds(5);

        private readonly IConnectionMultiplexer _redis;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RedisWriteRoutingMarkerStore(
            IConnectionMultiplexer redis,
            IHttpContextAccessor httpContextAccessor)
        {
            _redis = redis;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<bool> HasRecentWriteAsync(
            CancellationToken cancellationToken = default)
        {
            var keys = GetClientKeys();
            if (keys.Count == 0)
            {
                return false;
            }

            var db = _redis.GetDatabase();

            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await db.KeyExistsAsync(key))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task MarkWriteAsync(
            CancellationToken cancellationToken = default)
        {
            var keys = GetClientKeys();
            if (keys.Count == 0)
            {
                return;
            }

            var db = _redis.GetDatabase();

            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await db.StringSetAsync(key, "1", MarkerTtl);
            }
        }

        private List<RedisKey> GetClientKeys()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null)
            {
                return [];
            }

            var keys = new List<RedisKey>();

            var userId =
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");

            if (!string.IsNullOrWhiteSpace(userId))
            {
                keys.Add($"{KeyPrefix}:user:{userId}");
            }

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "local";
            keys.Add($"{KeyPrefix}:ip:{ip}");

            return keys;
        }
    }
}