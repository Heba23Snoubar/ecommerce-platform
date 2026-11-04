    using Catalog.Module.Application.Common.Results;
using Products.Module.Application.DTOs.Categories;

namespace Catalog.Module.Application.Services.Interfaces
{

   
    public interface ICategoryService
    {
        Task<ServiceResult<CategoryDto>> CreateAsync(
            CreateCategoryRequest request,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<CategoryDto>> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<IReadOnlyList<CategoryDto>>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<ServiceResult<CategoryDto>> UpdateAsync(
            Guid id,
            UpdateCategoryRequest request,
            CancellationToken cancellationToken = default);

        Task<ServiceResult> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}
