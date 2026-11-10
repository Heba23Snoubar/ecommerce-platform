using Catalog.Module.Application.Abstraction.Interfaces;
using Catalog.Module.Application.Abstraction.Repositories;
using Catalog.Module.Application.Common.Models;
using Catalog.Module.Application.Common.Results;
using Catalog.Module.Application.DTOs.Products;
using Catalog.Module.Application.Services.Interfaces;
using E.Domain.Entites;

namespace Catalog.Module.Application.Services.Implementations
{

 

    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ProductService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IUnitOfWork unitOfWork)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
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

            return ServiceResult<Guid>.Success(product.Id);
        }

        // GET BY ID
        public async Task<ServiceResult<ProductDetailsDto>> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
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

            return ServiceResult<ProductDetailsDto>.Success(result);
        }

        // GET PAGED
        public async Task<ServiceResult<PagedResult<ProductListDto>>> GetPagedAsync(
            ProductFilter filter,
            CancellationToken cancellationToken = default)
        {
            var result = await _productRepository.GetPagedAsync(
                filter,
                cancellationToken);

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

            return ServiceResult.Success();
        }

        // CHANGE PRICE
        public async Task<ServiceResult> ChangePriceAsync(
            Guid id,
            decimal newPrice,
            CancellationToken cancellationToken = default)
        {
            if (newPrice <= 0)
                return ServiceResult<ProductDetailsDto> .Failure(
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

            return ServiceResult.Success();
        }
    }
}
