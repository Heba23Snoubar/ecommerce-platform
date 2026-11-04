using E.Domain.Entities;




namespace Catalog.Module.Application.Abstraction.Repositories
{


 
    public interface ICategoryRepository
    {
        Task<IReadOnlyCollection<Category>> GetByIdsAsync(
    IReadOnlyCollection<Guid> ids,
    CancellationToken cancellationToken = default);

        Task<Category?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Category>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<bool> ExistsWithNameAsync(
            string name,
            Guid? parentId,
            Guid? excludeCategoryId = null,
            CancellationToken cancellationToken = default);

        Task<bool> HasChildrenAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default);

        Task<bool> HasProductsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default);

        Task<bool> IsDescendantOfAsync(
            Guid categoryId,
            Guid ancestorId,
            CancellationToken cancellationToken = default);

        void Add(Category category);

        Task<bool> ExistsAsync(
    Guid id,
    CancellationToken cancellationToken = default);
        void Remove(Category category);
    }

}
