using E.Domain.Entites;
using E.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace E.Infrastructure.Persistence
{

    public class CatalogDbContext  : DbContext
    {
        public CatalogDbContext (DbContextOptions<CatalogDbContext > options)
            : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();

        public DbSet<Category> Categories => Set<Category>();


        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
         
 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.HasDefaultSchema("products");
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext ).Assembly);
        }
    
    }
}
