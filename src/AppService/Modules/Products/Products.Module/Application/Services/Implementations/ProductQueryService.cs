using Catalog.Module.Application.Abstraction.Repositories;
using E.Domain.Entites;
using Products.Contract.Product.DTOs;
using Products.Contract.Product.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Module.Application.Services.Implementations
{ 

    public class ProductQueryService : IProductQueryService
    {
        private readonly IProductRepository _productRepository;

        public ProductQueryService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<ProductInfoDto?> GetByIdAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdAsync(
                productId,
                cancellationToken);

            return product is null ? null : MapToDto(product);
        }

        public async Task<IReadOnlyCollection<ProductInfoDto>> GetByIdsAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken = default)
        {
            if (productIds.Count == 0)
                return Array.Empty<ProductInfoDto>();

            var products = await _productRepository.GetByIdsAsync(
                productIds,
                cancellationToken);

            return products
                .Select(MapToDto)
                .ToList();
        }

        private static ProductInfoDto MapToDto(Product product)
        {
            var images = product.Images
                .Select(image => new ProductImageDto(
                    image.Id,
                    image.ImageUrl,
                    image.IsMain))
                .ToList();

            return new ProductInfoDto(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.CategoryId,
                product.IsAvailable,
                images);
        }
    }
}
