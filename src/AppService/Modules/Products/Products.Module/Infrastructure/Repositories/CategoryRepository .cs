using Catalog.Module.Application.Abstraction.Repositories;
using E.Domain.Entities;
using E.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Catalog.Module.Infrastructure.Repositories
{


  

    public class CategoryRepository : ICategoryRepository
    {
        private readonly CatalogDbContext _context;

        public CategoryRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<Category?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(
                    c => c.Id == id,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<Category>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsWithNameAsync(
            string name,
            Guid? parentId,
            Guid? excludeCategoryId = null,
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories.AnyAsync(
                c => c.Name == name
                     && c.ParentId == parentId
                     && (!excludeCategoryId.HasValue ||
                         c.Id != excludeCategoryId.Value),
                cancellationToken);
        }

        public async Task<bool> HasChildrenAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories.AnyAsync(
                c => c.ParentId == categoryId,
                cancellationToken);
        }

        public async Task<bool> HasProductsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Products.AnyAsync(
                p => p.CategoryId == categoryId,
                cancellationToken);
        }

        public async Task<bool> IsDescendantOfAsync(
            Guid categoryId,
            Guid ancestorId,
            CancellationToken cancellationToken = default)
        {
            var currentParentId = await _context.Categories
                .Where(c => c.Id == categoryId)
                .Select(c => c.ParentId)
                .FirstOrDefaultAsync(cancellationToken);

            while (currentParentId.HasValue)
            {
                if (currentParentId.Value == ancestorId)
                    return true;

                currentParentId = await _context.Categories
                    .Where(c => c.Id == currentParentId.Value)
                    .Select(c => c.ParentId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return false;
        }

        public void Add(Category category)
        {
            _context.Categories.Add(category);
        }

        public void Remove(Category category)
        {
            _context.Categories.Remove(category);
        }
        public async Task<bool> ExistsAsync(
    Guid id,
    CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AnyAsync(c => c.Id == id, cancellationToken);
        }
    }
}
