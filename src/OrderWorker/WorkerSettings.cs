namespace OrderWorker;

public sealed class RabbitMqSettings
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public static RabbitMqSettings FromEnvironment()
    {
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        var portRaw = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
        var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? string.Empty;
        var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(user) || user.Equals("guest", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "RABBITMQ_USER environment variable must be set and cannot be 'guest'.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("RABBITMQ_PASSWORD environment variable is required.");
        }

        return new RabbitMqSettings
        {
            Host = host,
            Port = int.TryParse(portRaw, out var port) ? port : 5672,
            User = user,
            Password = password
        };
    }
}

public sealed class SmtpSettings
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;

    public static SmtpSettings FromEnvironment()
    {
        var host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? string.Empty;
        var portRaw = Environment.GetEnvironmentVariable("SMTP_PORT");
        var user = Environment.GetEnvironmentVariable("SMTP_USER") ?? string.Empty;
        var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? string.Empty;
        var from = Environment.GetEnvironmentVariable("SMTP_FROM") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("SMTP_HOST environment variable is required.");

        if (string.IsNullOrWhiteSpace(user))
            throw new InvalidOperationException("SMTP_USER environment variable is required.");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("SMTP_PASSWORD environment variable is required.");

        return new SmtpSettings
        {
            Host = host.Trim(),
            Port = int.TryParse(portRaw, out var port) ? port : 587,
            User = user.Trim(),
            Password = password.Trim(),
            From = string.IsNullOrWhiteSpace(from) ? user.Trim() : from.Trim()
        };
    }
}
public sealed class RedisSettings
{
    public string Host { get; init; } = "redis";

    public int Port { get; init; } = 6379;
    public string? Password { get; init; }

    public string KeyPrefix { get; init; } = "sent:";

    public TimeSpan Ttl { get; init; } = TimeSpan.FromDays(7);

    public static RedisSettings FromEnvironment()
    {
        var host = Environment.GetEnvironmentVariable("REDIS_HOST");
        var portRaw = Environment.GetEnvironmentVariable("REDIS_PORT");
        var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
        var ttlHoursRaw = Environment.GetEnvironmentVariable("REDIS_IDEMPOTENCY_TTL_HOURS");
        var resolvedHost = string.IsNullOrWhiteSpace(host) ? "redis" : host.Trim();
        var resolvedPort = int.TryParse(portRaw, out var port) && port > 0 ? port : 6379;
        var resolvedPassword = string.IsNullOrWhiteSpace(password) ? null : password.Trim();
        var ttlHours = int.TryParse(ttlHoursRaw, out var hours) && hours > 0 ? hours : 168;

        return new RedisSettings
        {
            Host = resolvedHost,
            Port = resolvedPort,
            Password = resolvedPassword,
            KeyPrefix = "sent:",
            Ttl = TimeSpan.FromHours(ttlHours)
        };
    }

    public string BuildConnectionString()
    {
        var parts = new List<string>
        {
            $"{Host}:{Port}",
            "abortConnect=false",
            "connectTimeout=3000"
        };

        if (!string.IsNullOrWhiteSpace(Password))
        {
            parts.Add($"password={Password}");
        }

        return string.Join(",", parts);
    }
}