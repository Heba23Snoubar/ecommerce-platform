using Catalog.Module.Application.Abstraction.Repositories;
using E.Domain.Entities;
using Products.Contract.Category.DTOs;
using Products.Contract.Category.Interfaces;

namespace Products.Module.Application.Services.Implementations
{
   

    public class CategoryQueryService : ICategoryQueryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryQueryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<CategoryInfoDto?> GetByIdAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(
                categoryId,
                cancellationToken);

            return category is null ? null : MapToDto(category);
        }

        public async Task<IReadOnlyCollection<CategoryInfoDto>> GetByIdsAsync(
            IReadOnlyCollection<Guid> categoryIds,
            CancellationToken cancellationToken = default)
        {
            if (categoryIds.Count == 0)
                return Array.Empty<CategoryInfoDto>();

            var categories = await _categoryRepository.GetByIdsAsync(
                categoryIds,
                cancellationToken);

            return categories
                .Select(MapToDto)
                .ToList();
        }

        private static CategoryInfoDto MapToDto(Category category)
        {
            return new CategoryInfoDto(
                category.Id,
                category.Name,
                category.Description,
                category.ParentId,
                category.ImageUrl);
        }
    }
}
