using Catalog.Module.Application.Abstraction.Interfaces;
using E.Infrastructure.Persistence;





namespace Catalog.Module.Infrastructure.Persistence
{
    
     
    public class UnitOfWork : IUnitOfWork
    {
        private readonly CatalogDbContext  _context;

        public UnitOfWork(CatalogDbContext  context)
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
