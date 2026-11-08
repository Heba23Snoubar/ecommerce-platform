using E.Domain.Entites;

namespace Catalog.Module.Application.Abstraction.Repositories
{


     
    public interface ICartRepository
    {
         Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

         Task<Cart?> GetByIdAsync(Guid cartId, CancellationToken ct = default);

         Task AddAsync(Cart cart, CancellationToken ct = default);

         void Remove(Cart cart);
    }
}
