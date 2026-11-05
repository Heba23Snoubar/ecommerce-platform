using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Users.Module.Data;

internal sealed class RbacSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RbacSeedHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<UsersDbContext>();

        await RbacSeeder.SeedAsync(dbContext, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}