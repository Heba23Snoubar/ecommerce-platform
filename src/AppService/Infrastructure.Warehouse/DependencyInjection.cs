using Contracts.Warehouse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
 
namespace Infrastructure.Warehouse;

public static class DependencyInjection
{
    public static IServiceCollection AddWarehouseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var warehouseUrl = configuration["Warehouse:Url"]
            ?? throw new InvalidOperationException("Configuration 'Warehouse:Url' is missing.");

        services.AddGrpcClient<WarehouseService.WarehouseServiceClient>(options =>
        {
            options.Address = new Uri(warehouseUrl);
        });

        services.AddScoped<IWarehouseService, WarehouseGrpcAdapter>();
        return services;
    }
}