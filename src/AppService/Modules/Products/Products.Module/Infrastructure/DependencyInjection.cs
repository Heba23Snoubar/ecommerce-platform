


using Catalog.Module.Application.Abstraction.Interfaces;
using Catalog.Module.Application.Abstraction.Repositories;
using Catalog.Module.Application.Services.Implementations;
using Catalog.Module.Application.Services.Interfaces;
using Catalog.Module.Infrastructure.Cache;
using Catalog.Module.Infrastructure.Persistence;
using Catalog.Module.Infrastructure.Repositories;
using E.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Products.Contract.Category.Interfaces;
using Products.Contract.Product.Interfaces;
using Products.Module.Application.Services.Implementations;
using Shared.Database;
using StackExchange.Redis;

namespace Catalog.Module.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddCatalogInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddModulePostgresDbContext<CatalogDbContext>(configuration, "CatalogDb", "products");

            // Redis Integration (Role C)
            var redisConn = configuration["REDIS_CONNECTION_STRING"]
                ?? configuration.GetConnectionString("Redis")
                ?? $"{configuration["REDIS_HOST"] ?? "localhost"}:{configuration["REDIS_PORT"] ?? "6379"},abortConnect=false,connectTimeout=3000";

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();
                var options = ConfigurationOptions.Parse(redisConn);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 3000;
                options.AsyncTimeout = 3000;
                options.SyncTimeout = 3000;

                try
                {
                    var multiplexer = ConnectionMultiplexer.Connect(options);
                    logger.LogInformation("Connected to Redis at {Endpoints}", string.Join(",", options.EndPoints));
                    return multiplexer;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed initial connection to Redis at {RedisConn}. Running in degraded mode with background retry on configured endpoints.", redisConn);
                    options.AbortOnConnectFail = false;
                    return ConnectionMultiplexer.Connect(options);
                }
            });

            services.AddScoped<IProductCache, ProductCache>();

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
