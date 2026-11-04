using Catalog.Module.Application.Common.Models;
using E.Domain.Entites;
using Catalog.Module.Application.DTOs.Products;

namespace Catalog.Module.Application.Abstraction.Repositories
{

 
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<Product>> GetByIdsAsync(
    IReadOnlyCollection<Guid> ids,
    CancellationToken cancellationToken = default);

        Task<PagedResult<ProductListDto>> GetPagedAsync(
            ProductFilter filter,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default);

        void Remove(Product product);

        Task<bool> ExistsAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}
