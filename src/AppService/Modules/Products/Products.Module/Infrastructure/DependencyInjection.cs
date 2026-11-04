


using Catalog.Module.Application.Abstraction.Interfaces;
using Catalog.Module.Application.Abstraction.Repositories;
using Catalog.Module.Application.Services.Implementations;
using Catalog.Module.Application.Services.Interfaces;
using Catalog.Module.Infrastructure.Persistence;
using Catalog.Module.Infrastructure.Repositories;
using E.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Products.Contract.Category.Interfaces;
using Products.Contract.Product.Interfaces;
using Products.Module.Application.Services.Implementations;

namespace Catalog.Module.Infrastructure
{
     

    public static class DependencyInjection
    {
        public static IServiceCollection AddCatalogInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
         
            services.AddDbContext<CatalogDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("CatalogDb"), npgsqlOptions =>
                {
                   
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "products");
                });
            });

            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();


            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();

            services.AddScoped<IProductQueryService, ProductQueryService>();
            services.AddScoped<ICategoryQueryService, CategoryQueryService>();

            return services;
        }
    }
}
