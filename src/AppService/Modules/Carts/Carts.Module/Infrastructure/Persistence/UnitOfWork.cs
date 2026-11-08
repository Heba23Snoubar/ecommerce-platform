using Carts.Module.Infrastructure.Persistence;
using Catalog.Module.Application.Abstraction.Interfaces;





namespace Catalog.Module.Infrastructure.Persistence
{
    
     
    public class UnitOfWork : IUnitOfWork
    {
        private readonly CartDbContext  _context;

        public UnitOfWork(CartDbContext  context)
        {
            _context = context;
        }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
