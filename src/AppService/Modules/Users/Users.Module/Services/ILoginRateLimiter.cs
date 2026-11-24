namespace Users.Module.Services;

public interface ILoginRateLimiter
{
    Task<RateLimitResult> CheckAsync(
        string ipAddress,
        CancellationToken cancellationToken);
}

public sealed record RateLimitResult(
    bool IsAllowed,
    int RetryAfterSeconds);