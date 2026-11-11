using Catalog.Module.Application.Abstraction.Interfaces;
using Catalog.Module.Application.Abstraction.Repositories;
using Catalog.Module.Application.Common.Models;
using Catalog.Module.Application.Common.Results;
using Catalog.Module.Application.DTOs.Products;
using Catalog.Module.Application.Services.Interfaces;
using E.Domain.Entites;
using Microsoft.Extensions.Logging;
using Shared.Database;

namespace Catalog.Module.Application.Services.Implementations
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProductCache _cache;
        private readonly ILogger<ProductService> _logger;
        private readonly IDatabaseRoutingContext? _databaseRoutingContext;

        public ProductService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IUnitOfWork unitOfWork,
            IProductCache cache,
            ILogger<ProductService> logger,
            IDatabaseRoutingContext? databaseRoutingContext = null)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
            _cache = cache;
            _logger = logger;
            _databaseRoutingContext = databaseRoutingContext;
        }

        // CREATE
        public async Task<ServiceResult<Guid>> CreateAsync(
            CreateProductRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult<Guid>.Failure("Product name is required.");

            if (request.Price <= 0)
                return ServiceResult<Guid>.Failure("Price must be greater than zero.");

            if (request.Images is null)
                return ServiceResult<Guid>.Failure("Images collection is required.");

            if (request.Images.Count(i => i.IsMain) > 1)
                return ServiceResult<Guid>.Failure("Only one main image is allowed.");

            var categoryExists = await _categoryRepository.ExistsAsync(
                request.CategoryId,
                cancellationToken);

            if (!categoryExists)
                return ServiceResult<Guid>.Failure("Category does not exist.");

            var product = new Product(
                request.Name,
                request.Price,
                request.CategoryId,
                request.Description);

            foreach (var image in request.Images)
            {
                product.AddImage(image.ImageUrl, image.IsMain);
            }

            await _productRepository.AddAsync(product, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // CACHE INVALIDATION: strictly after SaveChanges succeeds without failing write operation
            try
            {
                _logger.LogInformation("[CACHE-ACTION] Product created with ID {ProductId}. Triggering cache invalidation.", product.Id);
                await _cache.InvalidateProductAsync(product.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed for created product {ProductId}. Database write succeeded.", product.Id);
            }

            return ServiceResult<Guid>.Success(product.Id);
        }

        // GET BY ID (Cache-Aside pattern)
        public async Task<ServiceResult<ProductDetailsDto>> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            // 1. Try reading from Redis Cache
            var cached = await _cache.GetProductDetailAsync(id, cancellationToken);
            if (cached is not null)
            {
                return ServiceResult<ProductDetailsDto>.Success(cached);
            }

            // 2. Cache Miss: check if a write occurred in the last 5 seconds to route to Primary DB
            if (await _cache.WasWrittenWithinAsync(TimeSpan.FromSeconds(5), cancellationToken))
            {
                _databaseRoutingContext?.MarkWriteOperation();
            }

            // 3. Read from Database
            var product = await _productRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (product is null)
                return ServiceResult<ProductDetailsDto>.Failure(
                    "Product does not exist.",
                    ServiceErrorType.NotFound);

            var result = new ProductDetailsDto(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.CategoryId,
                product.IsAvailable,
                product.Images.Select(image => new ProductImageDto(
                    image.Id,
                    image.ImageUrl,
                    image.IsMain)).ToList(),
                product.CreatedAt,
                product.UpdatedAt);

            // 4. Fill Redis Cache (TTL: 5 minutes)
            await _cache.SetProductDetailAsync(id, result, cancellationToken);

            return ServiceResult<ProductDetailsDto>.Success(result);
        }

        // GET PAGED (Versioned Cache-Aside pattern)
        public async Task<ServiceResult<PagedResult<ProductListDto>>> GetPagedAsync(
            ProductFilter filter,
            CancellationToken cancellationToken = default)
        {
            // 1. Fetch current list version (O(1))
            var version = await _cache.GetListVersionAsync(cancellationToken);
            if (!version.HasValue)
            {
                // Reading list version failed: bypass cache and force Primary DB routing
                _databaseRoutingContext?.MarkWriteOperation();
                var dbList = await _productRepository.GetPagedAsync(filter, cancellationToken);
                return ServiceResult<PagedResult<ProductListDto>>.Success(dbList);
            }

            // 2. Try reading from Redis Cache under current version
            var cached = await _cache.GetProductListAsync(filter, version.Value, cancellationToken);
            if (cached is not null)
            {
                return ServiceResult<PagedResult<ProductListDto>>.Success(cached);
            }

            // 3. Cache Miss: check if a write occurred in the last 5 seconds to route to Primary DB
            if (await _cache.WasWrittenWithinAsync(TimeSpan.FromSeconds(5), cancellationToken))
            {
                _databaseRoutingContext?.MarkWriteOperation();
            }

            // 4. Read from Database
            var result = await _productRepository.GetPagedAsync(
                filter,
                cancellationToken);

            // 5. Fill Redis Cache (TTL: 60 seconds)
            await _cache.SetProductListAsync(filter, version.Value, result, cancellationToken);

            return ServiceResult<PagedResult<ProductListDto>>.Success(result);
        }

        // UPDATE
        public async Task<ServiceResult> UpdateAsync(
            Guid id,
            UpdateProductRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult<ProductDetailsDto>.Failure(
                    "Product does not exist.",
                    ServiceErrorType.NotFound);

            if (request.Price <= 0)
                return ServiceResult<ProductDetailsDto>.Failure(
                    "Product does not exist.",
                    ServiceErrorType.NotFound);

            var product = await _productRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (product is null)
                return ServiceResult<ProductDetailsDto>.Failure(
                    "Product does not exist.",
                    ServiceErrorType.NotFound);

            var categoryExists = await _categoryRepository.ExistsAsync(
                request.CategoryId,
                cancellationToken);

            if (!categoryExists)
                return ServiceResult<ProductDetailsDto>.Failure(
                    "Product does not exist.",
                    ServiceErrorType.NotFound);

            product.Rename(request.Name);
            product.ChangeDescription(request.Description);
            product.ChangePrice(request.Price);
            product.ChangeCategory(request.CategoryId);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // CACHE INVALIDATION: strictly after SaveChanges succeeds without failing write operation
            try
            {
                _logger.LogInformation("[CACHE-ACTION] Product updated with ID {ProductId}. Triggering cache invalidation.", id);
                await _cache.InvalidateProductAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed for updated product {ProductId}. Database write succeeded.", id);
            }

            return ServiceResult.Success();
        }

        // CHANGE PRICE
        public async Task<ServiceResult> ChangePriceAsync(
            Guid id,
            decimal newPrice,
            CancellationToken cancellationToken = default)
        {
            if (newPrice <= 0)
                return ServiceResult<ProductDetailsDto>.Failure(
                    "Product does not exist.",
                    ServiceErrorType.NotFound);

            var product = await _productRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (product is null)
                return ServiceResult.Failure(
                    "Product does not exist.",
                    ServiceErrorType.NotFound);

            product.ChangePrice(newPrice);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // CACHE INVALIDATION: strictly after SaveChanges succeeds without failing write operation
            try
            {
                _logger.LogInformation("[CACHE-ACTION] Product price changed for ID {ProductId} to {Price}. Triggering cache invalidation.", id, newPrice);
                await _cache.InvalidateProductAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed for price change on product {ProductId}. Database write succeeded.", id);
            }

            return ServiceResult.Success();
        }

        // DELETE
        public async Task<ServiceResult> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (product is null)
                return ServiceResult<ProductDetailsDto>.Failure("Product does not exist.", ServiceErrorType.NotFound);

            _productRepository.Remove(product);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // CACHE INVALIDATION: strictly after SaveChanges succeeds without failing write operation
            try
            {
                _logger.LogInformation("[CACHE-ACTION] Product deleted with ID {ProductId}. Triggering cache invalidation.", id);
                await _cache.InvalidateProductAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed for deleted product {ProductId}. Database write succeeded.", id);
            }

            return ServiceResult.Success();
        }
    }
}
