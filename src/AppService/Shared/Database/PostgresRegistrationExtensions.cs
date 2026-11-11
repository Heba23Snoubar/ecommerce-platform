using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Database
{


    
     
    public static class PostgresRegistrationExtensions
    {
        public static IServiceCollection AddDatabaseReplicaRouting(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            services.AddScoped<IDatabaseRoutingContext, DatabaseRoutingContext>();
            return services;
        }

        public static IApplicationBuilder UseReadYourOwnWritesRouting(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ReadYourOwnWritesMiddleware>();
        }

        public static IServiceCollection AddModulePostgresDbContext<TDbContext>(
            this IServiceCollection services,
            IConfiguration configuration,
            string connectionStringName,
            string schemaName)
            where TDbContext : DbContext
        {
            services.AddDbContext<TDbContext>((sp, options) =>
            {
                var primaryConn = configuration.GetConnectionString(connectionStringName);
                if (string.IsNullOrWhiteSpace(primaryConn))
                {
                    throw new InvalidOperationException(
                        $"Connection string '{connectionStringName}' is not configured.");
                }

                var replicaConn = configuration.GetConnectionString($"{connectionStringName}Replica");

                var routing = sp.GetService<IDatabaseRoutingContext>();
                var finalConn = routing is not null
                    ? routing.ResolveConnectionString(primaryConn, replicaConn)
                    : primaryConn;

                options.UseNpgsql(finalConn, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
                });
            });

            return services;
        }
    }


}
