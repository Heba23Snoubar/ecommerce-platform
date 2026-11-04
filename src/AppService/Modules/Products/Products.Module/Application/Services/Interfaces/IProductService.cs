using Catalog.Module.Application.Common.Models;
using Catalog.Module.Application.Common.Results;
using Catalog.Module.Application.DTOs.Products;
 



namespace Catalog.Module.Application.Services.Interfaces
{

    
    public interface IProductService
    {
        Task<ServiceResult<Guid>> CreateAsync(
            CreateProductRequest request,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<ProductDetailsDto>> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<PagedResult<ProductListDto>>> GetPagedAsync(
            ProductFilter filter,
            CancellationToken cancellationToken = default);

        Task<ServiceResult> UpdateAsync(
            Guid id,
            UpdateProductRequest request,
            CancellationToken cancellationToken = default);

        Task<ServiceResult> ChangePriceAsync(
            Guid id,
            decimal newPrice,
            CancellationToken cancellationToken = default);

        Task<ServiceResult> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}
