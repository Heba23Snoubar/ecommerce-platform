using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Contracts;
using Orders.Module.Data;
using Orders.Module.Ports;
using Orders.Module.Services;
using Orders.Module.Stubs;

namespace Orders.Module;

public static class OrdersModule
{
    /// الـ Assembly الذي يحوي Controllers الوحدة، يستعمله Host في AddApplicationPart
    public static Assembly ModuleAssembly => typeof(OrdersModule).Assembly;

    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<OrdersDbContext>(o => o.UseNpgsql(
            config.GetConnectionString("Orders"),
            n => n.MigrationsHistoryTable("__EFMigrationsHistory", "orders")));

        services.AddScoped<IOrdersApi, OrdersService>();

        // نسخ وهمية مؤقتة، تُستبدل لاحقاً بمحوّلات إلى واجهات M3 وM1
        services.AddSingleton<IProductCatalog, FakeProductCatalog>();
        services.AddSingleton<IStockReservation, FakeStockReservation>();
        return services;
    }

    public static void MigrateOrdersModule(this IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.Migrate();
    }
}