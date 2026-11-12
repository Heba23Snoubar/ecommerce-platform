using Catalog.Module.Application.Common.Models;
using Catalog.Module.Application.DTOs.Products;
using Products.Module.Application.DTOs.Categories;

namespace Catalog.Module.Application.Services.Interfaces
{
    /// <summary>
    /// Abstraction for Redis-based catalog caching (Role C).
    /// Handles product details, versioned product lists, categories, and replication write-tracking.
    /// </summary>
    public interface IProductCache
    {
        /// <summary>
        /// Gets cached product detail: app:v1:products:detail:{id} (TTL: 5 minutes).
        /// </summary>
        Task<ProductDetailsDto?> GetProductDetailAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Stores product detail in cache with 5-minute TTL.
        /// </summary>
        Task SetProductDetailAsync(Guid id, ProductDetailsDto product, CancellationToken ct = default);

        /// <summary>
        /// Gets cached versioned product list: app:v1:products:list:v{version}:{hash(normalized-query)} (TTL: 60 seconds).
        /// </summary>
        Task<PagedResult<ProductListDto>?> GetProductListAsync(ProductFilter filter, int version, CancellationToken ct = default);

        /// <summary>
        /// Stores product list in cache with 60-second TTL under the specified version and normalized filter hash.
        /// </summary>
        Task SetProductListAsync(ProductFilter filter, int version, PagedResult<ProductListDto> result, CancellationToken ct = default);

        /// <summary>
        /// Gets current list version from key app:v1:products:list:version.
        /// Returns null if Redis query fails.
        /// </summary>
        Task<int?> GetListVersionAsync(CancellationToken ct = default);

        /// <summary>
        /// Cache invalidation executed ONLY AFTER successful SaveChanges:
        /// 1. INCR list version (app:v1:products:list:version).
        /// 2. DEL product detail (app:v1:products:detail:{id}).
        /// 3. Update products:last-write-ts.
        /// </summary>
        Task InvalidateProductAsync(Guid productId, CancellationToken ct = default);

        /// <summary>
        /// Invalidates only list queries by bumping version and updating last-write-ts.
        /// </summary>
        Task InvalidateListsOnlyAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets cached categories: app:v1:categories:all (TTL: 10 minutes).
        /// </summary>
        Task<IReadOnlyList<CategoryDto>?> GetCategoriesAsync(CancellationToken ct = default);

        /// <summary>
        /// Stores categories in cache with 10-minute TTL.
        /// </summary>
        Task SetCategoriesAsync(IReadOnlyList<CategoryDto> categories, CancellationToken ct = default);

        /// <summary>
        /// Invalidates categories cache and bumps product list version.
        /// </summary>
        Task InvalidateCategoriesAsync(CancellationToken ct = default);

        /// <summary>
        /// Checks whether a write occurred within the specified time window (e.g. 5 seconds)
        /// using the marker products:last-write-ts.
        /// </summary>
        Task<bool> WasWrittenWithinAsync(TimeSpan window, CancellationToken ct = default);

        /// <summary>
        /// Count of Redis operational errors encountered since process start.
        /// </summary>
        long RedisErrorCount { get; }
    }
}
