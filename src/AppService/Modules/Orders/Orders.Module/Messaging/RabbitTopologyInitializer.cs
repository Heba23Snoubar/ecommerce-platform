using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Orders.Module.Messaging;

internal sealed class RabbitTopologyInitializer(
    IConfiguration config, ILogger<RabbitTopologyInitializer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = config["RABBITMQ_HOST"] ?? "localhost",
            Port = int.TryParse(config["RABBITMQ_PORT"], out var p) ? p : 5672,
            UserName = config["RABBITMQ_USER"] ?? "app",
            Password = config["RABBITMQ_PASSWORD"] ?? ""
        };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(ct);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
                await OrdersTopology.DeclareAsync(channel, ct);
                log.LogInformation("RabbitMQ topology declared.");
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                log.LogWarning(ex, "RabbitMQ not reachable, retrying in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}