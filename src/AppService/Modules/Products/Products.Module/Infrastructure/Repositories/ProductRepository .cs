using Catalog.Module.Application.Abstraction.Repositories;
using Catalog.Module.Application.Common.Models;
using Catalog.Module.Application.DTOs.Products;
using E.Domain.Entites;
using E.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;




namespace Catalog.Module.Infrastructure.Repositories
{

 
    public class ProductRepository : IProductRepository
    {
        private readonly CatalogDbContext  _context;

        public ProductRepository(CatalogDbContext  context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(
                    p => p.Id == id,
                    cancellationToken);
        }

        public async Task<PagedResult<ProductListDto>> GetPagedAsync(
      ProductFilter filter,
      CancellationToken cancellationToken = default)
        {
            ValidateFilter(filter);

            IQueryable<Product> query = _context.Products.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.Trim();

                query = query.Where(p => p.Name.Contains(searchTerm));
            }

            if (filter.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

            if (filter.MinPrice.HasValue)
                query = query.Where(p => p.Price >= filter.MinPrice.Value);

            if (filter.MaxPrice.HasValue)
                query = query.Where(p => p.Price <= filter.MaxPrice.Value);

            if (filter.IsAvailable.HasValue)
                query = query.Where(p => p.IsAvailable == filter.IsAvailable.Value);

            var totalCount = await query.CountAsync(cancellationToken);

            query = ApplySorting(query, filter);

            var products = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(p => new ProductListDto(
                    p.Id,
                    p.Name,
                    p.Price,
                    p.CategoryId,
                    p.Images
                        .Where(i => i.IsMain)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault(),
                    p.IsAvailable))
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductListDto>(
                products,
                filter.PageNumber,
                filter.PageSize,
                totalCount);
        } 
        public async Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default)
        {
            await _context.Products.AddAsync(
                product,
                cancellationToken);
        }

        public void Remove(Product product)
        {
            _context.Products.Remove(product);
        }

        public Task<bool> ExistsAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return _context.Products.AnyAsync(
                p => p.Id == id,
                cancellationToken);
        }

        private static IQueryable<Product> ApplySorting(
            IQueryable<Product> query,
            ProductFilter filter)
        {
            return filter.SortBy?.ToLowerInvariant() switch
            {
                "name" => filter.SortDescending
                    ? query.OrderByDescending(p => p.Name).ThenBy(p => p.Id)
                    : query.OrderBy(p => p.Name).ThenBy(p => p.Id),

                "price" => filter.SortDescending
? query.OrderByDescending(p => p.Price).ThenBy(p => p.Id)
: query.OrderBy(p => p.Price).ThenBy(p => p.Id),

                "createdat" => filter.SortDescending
                    ? query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
                    : query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),

                _ => query.OrderBy(p => p.Id)
            };
        }

        private static void ValidateFilter(ProductFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);

            if (filter.PageNumber < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(filter.PageNumber));

            if (filter.PageSize is < 1 or > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(filter.PageSize));

            if (filter.MinPrice < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(filter.MinPrice));

            if (filter.MaxPrice < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(filter.MaxPrice));

            if (filter.MinPrice.HasValue &&
                filter.MaxPrice.HasValue &&
                filter.MinPrice > filter.MaxPrice)
            {
                throw new ArgumentException(
                    "Minimum price cannot exceed maximum price.");
            }
        }
    }
}

