
using Carts.Contract.Cart.Interfaces;
using Carts.Module.Application.Services.Implementations;
using Carts.Module.Infrastructure.Persistence;
using Carts.Module.Infrastructure.Repositories;
using Catalog.Module.Application.Abstraction.Interfaces;
using Catalog.Module.Application.Abstraction.Repositories;
using Catalog.Module.Application.Services.Interfaces;
using Catalog.Module.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
 
namespace Carts.Module.Infrastructure
{
     

    public static class DependencyInjection
    {
        public static IServiceCollection AddCartInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
         
            services.AddDbContext<CartDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("CartDb"), npgsqlOptions =>
                {
                   
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "cart");
                });
            });

            services.AddScoped<ICartRepository, CartRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<ICartQueryService, CartQueryService>();
            return services;
        }
    }
}
