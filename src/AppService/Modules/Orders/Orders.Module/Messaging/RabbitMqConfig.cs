using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Orders.Module.Messaging;

internal static class RabbitMqConfig
{
    public static ConnectionFactory CreateFactory(IConfiguration config) => new()
    {
        HostName = config["RABBITMQ_HOST"] ?? "localhost",
        Port = int.TryParse(config["RABBITMQ_PORT"], out var p) ? p : 5672,
        UserName = config["RABBITMQ_USER"] ?? "app",
        Password = config["RABBITMQ_PASSWORD"] ?? "",
        RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
    };
}