using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Notifications.Module.Data;
using Notifications.Module.Services;

namespace Notifications.Module;

public static class NotificationsModuleExtensions
{
    public static Assembly ModuleAssembly =>
        typeof(NotificationsModuleExtensions).Assembly;

    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(
                config.GetConnectionString("Notifications"),
                npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "notifications"
                    )
            )
        );

        services.AddScoped<INotificationsApi, NotificationsService>();

        return services;
    }

    public static void MigrateNotificationsModule(
        this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<NotificationsDbContext>()
            .Database.Migrate();
    }
}