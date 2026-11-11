using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catalog.Module.Application.Common.Models;
using Catalog.Module.Application.DTOs.Products;
using Catalog.Module.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Products.Module.Application.DTOs.Categories;
using StackExchange.Redis;

namespace Catalog.Module.Infrastructure.Cache
{
    /// <summary>
    /// Redis implementation of IProductCache (Role C).
    /// Enforces standardized keys, TTLs, normalized query hashing, versioned list invalidation,
    /// write-timestamp tracking, and resilient error handling.
    /// </summary>
    public class ProductCache : IProductCache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<ProductCache> _logger;

        // Key standards
        private const string KeyPrefix = "app:v1";
        private const string ListVersionKey = "app:v1:products:list:version";
        private const string CategoriesKey = "app:v1:categories:all";
        private const string LastWriteTsKey = "products:last-write-ts";

        // TTL standards
        private static readonly TimeSpan ProductDetailTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ProductListTtl = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan CategoriesTtl = TimeSpan.FromMinutes(10);

        // JSON serializer options
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // Thread-safe Redis error counter
        private static long _errorCount;
        public long RedisErrorCount => Interlocked.Read(ref _errorCount);

        public ProductCache(
            IConnectionMultiplexer redis,
            ILogger<ProductCache> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        private static string ProductDetailKey(Guid id) => $"{KeyPrefix}:products:detail:{id:D}";
        private static string ProductListKey(int version, string queryHash) => $"{KeyPrefix}:products:list:v{version}:{queryHash}";

        // ==========================================
        // 1. PRODUCT DETAILS (TTL: 5 Minutes)
        // ==========================================

        public async Task<ProductDetailsDto?> GetProductDetailAsync(Guid id, CancellationToken ct = default)
        {
            var key = ProductDetailKey(id);
            try
            {
                var db = _redis.GetDatabase();
                var val = await db.StringGetAsync(key);
                if (!val.HasValue)
                {
                    _logger.LogInformation("[CACHE-MISS] Product detail for ID {ProductId} (Key: {Key})", id, key);
                    return null;
                }

                var dto = JsonSerializer.Deserialize<ProductDetailsDto>(val.ToString(), JsonOptions);
                _logger.LogInformation("[CACHE-HIT] Product detail for ID {ProductId} (Key: {Key})", id, key);
                return dto;
            }
            catch (Exception ex)
            {
                RecordError(ex, nameof(GetProductDetailAsync), key);
                return null; // Fallback to DB
            }
        }

        public async Task SetProductDetailAsync(Guid id, ProductDetailsDto product, CancellationToken ct = default)
        {
            var key = ProductDetailKey(id);
            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(product, JsonOptions);
                await db.StringSetAsync(key, json, ProductDetailTtl);
                _logger.LogDebug("[CACHE-SET] Stored product detail for ID {ProductId} (Key: {Key}, TTL: {Ttl}s)",
                    id, key, ProductDetailTtl.TotalSeconds);
            }
            catch (Exception ex)
            {
                RecordError(ex, nameof(SetProductDetailAsync), key);
            }
        }

        // ==========================================
        // 2. PRODUCT LISTS (Versioned, TTL: 60 Seconds)
        // ==========================================

        public async Task<PagedResult<ProductListDto>?> GetProductListAsync(
            ProductFilter filter,
            int version,
            CancellationToken ct = default)
        {
            var hash = ComputeFilterHash(filter);
            var key = ProductListKey(version, hash);

            try
            {
                var db = _redis.GetDatabase();
                var val = await db.StringGetAsync(key);
                if (!val.HasValue)
                {
                    _logger.LogInformation("[CACHE-MISS] Product list v{Version} (Hash: {Hash}, Key: {Key})", version, hash, key);
                    return null;
                }

                var cachedDto = JsonSerializer.Deserialize<PagedResultCacheModel<ProductListDto>>(val.ToString(), JsonOptions);
                if (cachedDto is null)
                {
                    return null;
                }

                _logger.LogInformation("[CACHE-HIT] Product list v{Version} (Hash: {Hash}, Total: {Total}, Key: {Key})",
                    version, hash, cachedDto.TotalCount, key);

                return new PagedResult<ProductListDto>(
                    cachedDto.Items,
                    cachedDto.PageNumber,
                    cachedDto.PageSize,
                    cachedDto.TotalCount);
            }
            catch (Exception ex)
            {
                RecordError(ex, nameof(GetProductListAsync), key);
                return null; // Fallback to DB
            }
        }

        public async Task SetProductListAsync(
            ProductFilter filter,
            int version,
            PagedResult<ProductListDto> result,
            CancellationToken ct = default)
        {
            var hash = ComputeFilterHash(filter);
            var key = ProductListKey(version, hash);

            try
            {
                var db = _redis.GetDatabase();
                var cacheModel = new PagedResultCacheModel<ProductListDto>(
                    result.Items,
                    result.PageNumber,
                    result.PageSize,
                    result.TotalCount);

                var json = JsonSerializer.Serialize(cacheModel, JsonOptions);
                await db.StringSetAsync(key, json, ProductListTtl);

                _logger.LogDebug("[CACHE-SET] Stored product list v{Version} (Hash: {Hash}, Count: {Count}, Key: {Key}, TTL: {Ttl}s)",
                    version, hash, result.Items.Count, key, ProductListTtl.TotalSeconds);
            }
            catch (Exception ex)
            {
                RecordError(ex, nameof(SetProductListAsync), key);
            }
        }

        public async Task<int?> GetListVersionAsync(CancellationToken ct = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var val = await db.StringGetAsync(ListVersionKey);
                if (val.HasValue && int.TryParse(val, out var v))
                {
                    return v;
                }

                // If not set yet, initialize to 1
                await db.StringSetAsync(ListVersionKey, 1, when: When.NotExists);
                return 1;
            }
            catch (Exception ex)
            {
                RecordError(ex, nameof(GetListVersionAsync), ListVersionKey);
                return null; // Return null so caller knows reading list version failed
            }
        }

        // ==========================================
        // 3. CACHE INVALIDATION
        // ==========================================

        public async Task InvalidateProductAsync(Guid productId, CancellationToken ct = default)
        {
            var detailKey = ProductDetailKey(productId);
            try
            {
                var db = _redis.GetDatabase();

                // 1. INCR list version to invalidate all cached list pages
                var newVersion = await db.StringIncrementAsync(ListVersionKey);

                // 2. Delete affected product detail
                await db.KeyDeleteAsync(detailKey);

                // 3. Update products:last-write-ts
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await db.StringSetAsync(LastWriteTsKey, nowMs.ToString());

                _logger.LogInformation(
                    "[CACHE-INVALIDATE] Invalidated product {ProductId}. List version incremented to {Version}. Updated {TsKey}={NowMs}.",
                    productId, newVersion, LastWriteTsKey, nowMs);
            }
            catch (Exception ex)
            {
                RecordInvalidationError(ex, nameof(InvalidateProductAsync), detailKey);
            }
        }

        public async Task InvalidateListsOnlyAsync(CancellationToken ct = default)
        {
            try
            {
                var db = _redis.GetDatabase();

                // 1. INCR list version
                var newVersion = await db.StringIncrementAsync(ListVersionKey);

                // 2. Update products:last-write-ts
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await db.StringSetAsync(LastWriteTsKey, nowMs.ToString());

                _logger.LogInformation(
                    "[CACHE-INVALIDATE] List version incremented to {Version}. Updated {TsKey}={NowMs}.",
                    newVersion, LastWriteTsKey, nowMs);
            }
            catch (Exception ex)
            {
                RecordInvalidationError(ex, nameof(InvalidateListsOnlyAsync), ListVersionKey);
            }
        }

        // ==========================================
        // 4. CATEGORIES (TTL: 10 Minutes)
        // ==========================================

        public async Task<IReadOnlyList<CategoryDto>?> GetCategoriesAsync(CancellationToken ct = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var val = await db.StringGetAsync(CategoriesKey);
                if (!val.HasValue)
                {
                    _logger.LogInformation("[CACHE-MISS] Categories list (Key: {Key})", CategoriesKey);
                    return null;
                }

                var list = JsonSerializer.Deserialize<List<CategoryDto>>(val.ToString(), JsonOptions);
                _logger.LogInformation("[CACHE-HIT] Categories list (Count: {Count}, Key: {Key})", list?.Count ?? 0, CategoriesKey);
                return list;
            }
            catch (Exception ex)
            {
                RecordError(ex, nameof(GetCategoriesAsync), CategoriesKey);
                return null;
            }
        }

        public async Task SetCategoriesAsync(IReadOnlyList<CategoryDto> categories, CancellationToken ct = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(categories, JsonOptions);
                await db.StringSetAsync(CategoriesKey, json, CategoriesTtl);
                _logger.LogDebug("[CACHE-SET] Stored categories list (Count: {Count}, Key: {Key}, TTL: {Ttl}s)",
                    categories.Count, CategoriesKey, CategoriesTtl.TotalSeconds);
            }
            catch (Exception ex)
            {
                RecordError(ex, nameof(SetCategoriesAsync), CategoriesKey);
            }
        }

        public async Task InvalidateCategoriesAsync(CancellationToken ct = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(CategoriesKey);
                await InvalidateListsOnlyAsync(ct);
                _logger.LogInformation("[CACHE-INVALIDATE] Categories cache deleted (Key: {Key}).", CategoriesKey);
            }
            catch (Exception ex)
            {
                RecordInvalidationError(ex, nameof(InvalidateCategoriesAsync), CategoriesKey);
            }
        }

        // ==========================================
        // 5. REPLICA WINDOW INTEGRATION (5 Seconds)
        // ==========================================

        public async Task<bool> WasWrittenWithinAsync(TimeSpan window, CancellationToken ct = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var val = await db.StringGetAsync(LastWriteTsKey);
                if (!val.HasValue || !long.TryParse(val, out var lastWriteMs))
                {
                    return false;
                }

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var elapsedMs = nowMs - lastWriteMs;
                var windowMs = (long)window.TotalMilliseconds;

                var isWithin = elapsedMs >= 0 && elapsedMs < windowMs;
                if (isWithin)
                {
                    _logger.LogInformation(
                        "[REPLICA-WINDOW] Last write was {ElapsedMs}ms ago (< {WindowMs}ms). Must read from Primary DB.",
                        elapsedMs, windowMs);
                }

                return isWithin;
            }
            catch (Exception ex)
            {
                RecordError(ex, nameof(WasWrittenWithinAsync), LastWriteTsKey);
                // When reading products:last-write-ts fails, force routing to Primary DB
                return true;
            }
        }

        // ==========================================
        // 6. QUERY NORMALIZATION & HASHING
        // ==========================================

        public static string ComputeFilterHash(ProductFilter filter)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var canonical = string.Concat(
                "cid=", filter.CategoryId?.ToString("D") ?? "",
                ";q=", filter.SearchTerm?.Trim().ToLowerInvariant() ?? "",
                ";min=", filter.MinPrice?.ToString("G", inv) ?? "",
                ";max=", filter.MaxPrice?.ToString("G", inv) ?? "",
                ";avail=", filter.IsAvailable.HasValue ? (filter.IsAvailable.Value ? "1" : "0") : "",
                ";sort=", filter.SortBy?.Trim().ToLowerInvariant() ?? "",
                ";desc=", filter.SortDescending ? "1" : "0",
                ";p=", filter.PageNumber.ToString(inv),
                ";ps=", filter.PageSize.ToString(inv)
            );

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
        }

        // ==========================================
        // 7. ERROR RECORDING & LOGGING
        // ==========================================

        private void RecordError(Exception ex, string operation, string key)
        {
            Interlocked.Increment(ref _errorCount);
            _logger.LogWarning(ex,
                "[REDIS-WARNING] Operation '{Operation}' failed on key '{Key}'. Total errors: {ErrorCount}. Falling back to database.",
                operation, key, RedisErrorCount);
        }

        private void RecordInvalidationError(Exception ex, string operation, string key)
        {
            Interlocked.Increment(ref _errorCount);
            _logger.LogError(ex,
                "[REDIS-ERROR] CRITICAL: Invalidation operation '{Operation}' failed on key '{Key}'. Total errors: {ErrorCount}. Cache may be stale until TTL.",
                operation, key, RedisErrorCount);
        }

        // DTO model for clean JSON serialization of PagedResult
        private record PagedResultCacheModel<T>(
            IReadOnlyList<T> Items,
            int PageNumber,
            int PageSize,
            int TotalCount);
    }
}
