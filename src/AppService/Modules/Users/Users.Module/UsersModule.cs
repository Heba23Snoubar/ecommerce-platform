using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Users.Module.Authorization;
using Users.Module.Data;
using Users.Module.Services;

namespace Users.Module;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Users");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Users database connection string is missing.");
        }

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql =>
                    npgsql.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "users"
                    )
            )
        );

        services.AddScoped<PasswordService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<AdminManagementService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddHostedService<RbacSeedHostedService>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        return services;
    }

    public static void MigrateUsersModule(
        this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<UsersDbContext>()
            .Database.Migrate();
    }
}
