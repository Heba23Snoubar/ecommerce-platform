using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Contracts;
using Orders.Module.Adapters;
using Orders.Module.Data;
using Orders.Module.Ports;
using Orders.Module.Services;
using Orders.Module.Stubs;
using Shared.Database;
using System.Reflection;

namespace Orders.Module;

public static class OrdersModule
{
    public static Assembly ModuleAssembly => typeof(OrdersModule).Assembly;

    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulePostgresDbContext<OrdersDbContext>(configuration, "Orders", "orders");



        services.AddScoped<IOrdersApi, OrdersService>();

        if (configuration.GetValue<bool>("Orders:UseFakeCatalog"))
            services.AddSingleton<IProductCatalog, FakeProductCatalog>();
        else
            services.AddScoped<IProductCatalog, ProductCatalogAdapter>();

        // المستودع: الحقيقي افتراضياً، والوهمي عند Orders:UseFakeWarehouse=true
        if (configuration.GetValue<bool>("Orders:UseFakeWarehouse"))
            services.AddSingleton<IStockReservation, FakeStockReservation>();
        else
            services.AddScoped<IStockReservation, WarehouseStockReservation>();

        services.AddScoped<ICartSource, CartSourceAdapter>();

        return services;
    }

    public static void MigrateOrdersModule(this IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.Migrate();
    }
}