using Catalog.Module.Application.Abstraction.Interfaces;
using Catalog.Module.Application.Abstraction.Repositories;
using Catalog.Module.Application.Common.Results;
using Catalog.Module.Application.Services.Interfaces;
using E.Domain.Entities;
using Microsoft.Extensions.Logging;
using Products.Module.Application.DTOs.Categories;

namespace Products.Module.Application.Services.Implementations
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProductCache _cache;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(
            ICategoryRepository categoryRepository,
            IUnitOfWork unitOfWork,
            IProductCache cache,
            ILogger<CategoryService> logger)
        {
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ServiceResult<CategoryDto>> CreateAsync(
            CreateCategoryRequest request,
            CancellationToken cancellationToken = default)
        {
            var name = request.Name.Trim();

            // Check duplicate name among siblings
            var exists = await _categoryRepository.ExistsWithNameAsync(
                name,
                request.ParentId,
                cancellationToken: cancellationToken);

            if (exists)
                return ServiceResult<CategoryDto>.Failure(
                    "A category with this name already exists under the same parent.",
                    ServiceErrorType.Conflict);

            // Validate parent
            if (request.ParentId.HasValue)
            {
                var parent = await _categoryRepository.GetByIdAsync(
                    request.ParentId.Value,
                    cancellationToken);

                if (parent is null)
                    return ServiceResult<CategoryDto>.Failure(
                        "Parent category was not found.",
                        ServiceErrorType.NotFound);
            }

            var category = new Category(
                request.Name,
                request.Description,
                request.ImageUrl,
                request.ParentId);

            _categoryRepository.Add(category);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Invalidate categories cache after write without failing write operation
            try
            {
                _logger.LogInformation("[CACHE-ACTION] Category created ({Name}). Invalidating categories cache.", name);
                await _cache.InvalidateCategoriesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed after creating category ({Name}). Write operation succeeded.", name);
            }

            return ServiceResult<CategoryDto>.Success(MapToDto(category));
        }

        public async Task<ServiceResult<CategoryDto>> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (category is null)
                return ServiceResult<CategoryDto>.Failure(
                    "Category was not found.",
                    ServiceErrorType.NotFound);

            return ServiceResult<CategoryDto>.Success(MapToDto(category));
        }

        public async Task<ServiceResult<IReadOnlyList<CategoryDto>>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            // 1. Try Cache (TTL: 10 minutes)
            var cached = await _cache.GetCategoriesAsync(cancellationToken);
            if (cached is not null)
            {
                return ServiceResult<IReadOnlyList<CategoryDto>>.Success(cached);
            }

            // 2. Read from Database
            var categories = await _categoryRepository.GetAllAsync(
                cancellationToken);

            var result = categories
                .Select(MapToDto)
                .ToList();

            // 3. Fill Cache
            await _cache.SetCategoriesAsync(result, cancellationToken);

            return ServiceResult<IReadOnlyList<CategoryDto>>.Success(result);
        }

        public async Task<ServiceResult<CategoryDto>> UpdateAsync(
            Guid id,
            UpdateCategoryRequest request,
            CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (category is null)
                return ServiceResult<CategoryDto>.Failure(
                    "Category was not found.",
                    ServiceErrorType.NotFound);

            var name = request.Name.Trim();

            // Check duplicate name, excluding current category
            var exists = await _categoryRepository.ExistsWithNameAsync(
                name,
                request.ParentId,
                id,
                cancellationToken);

            if (exists)
                return ServiceResult<CategoryDto>.Failure(
                    "A category with this name already exists under the same parent.",
                    ServiceErrorType.Conflict);

            // Validate parent existence
            if (request.ParentId.HasValue)
            {
                var parent = await _categoryRepository.GetByIdAsync(
                    request.ParentId.Value,
                    cancellationToken);

                if (parent is null)
                    return ServiceResult<CategoryDto>.Failure(
                        "Parent category was not found.",
                        ServiceErrorType.NotFound);

                // Prevent moving category under one of its descendants
                var createsCycle = await _categoryRepository.IsDescendantOfAsync(
                    request.ParentId.Value,
                    id,
                    cancellationToken);

                if (createsCycle)
                    return ServiceResult<CategoryDto>.Failure(
                        "A category cannot be moved under one of its descendants.",
                        ServiceErrorType.Conflict);
            }

            category.Rename(request.Name);
            category.ChangeDescription(request.Description);
            category.ChangeImage(request.ImageUrl);
            category.MoveToParent(request.ParentId);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Invalidate categories cache after write without failing write operation
            try
            {
                _logger.LogInformation("[CACHE-ACTION] Category updated ({CategoryId}). Invalidating categories cache.", id);
                await _cache.InvalidateCategoriesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed after updating category ({CategoryId}). Write operation succeeded.", id);
            }

            return ServiceResult<CategoryDto>.Success(MapToDto(category));
        }

        public async Task<ServiceResult> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (category is null)
                return ServiceResult.Failure(
                    "Category was not found.",
                    ServiceErrorType.NotFound);

            var hasChildren = await _categoryRepository.HasChildrenAsync(
                id,
                cancellationToken);

            if (hasChildren)
                return ServiceResult.Failure(
                    "Cannot delete a category that has child categories.",
                    ServiceErrorType.Conflict);

            var hasProducts = await _categoryRepository.HasProductsAsync(
                id,
                cancellationToken);

            if (hasProducts)
                return ServiceResult.Failure(
                    "Cannot delete a category that has products.",
                    ServiceErrorType.Conflict);

            _categoryRepository.Remove(category);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Invalidate categories cache after write without failing write operation
            try
            {
                _logger.LogInformation("[CACHE-ACTION] Category deleted ({CategoryId}). Invalidating categories cache.", id);
                await _cache.InvalidateCategoriesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed after deleting category ({CategoryId}). Write operation succeeded.", id);
            }

            return ServiceResult.Success();
        }

        private static CategoryDto MapToDto(Category category)
        {
            return new CategoryDto(
                category.Id,
                category.Name,
                category.Description,
                category.ImageUrl,
                category.ParentId,
                category.CreatedAt,
                category.UpdatedAt);
        }
    }
}
