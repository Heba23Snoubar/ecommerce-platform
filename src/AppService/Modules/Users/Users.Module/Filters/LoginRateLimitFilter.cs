using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Shared.Errors;
using Users.Contracts;
using Users.Module.Services;

namespace Users.Module.Filters;

public sealed class LoginRateLimitFilter : IAsyncResourceFilter
{
    private readonly ILoginRateLimiter _rateLimiter;
    private readonly ILogger<LoginRateLimitFilter> _logger;

    public LoginRateLimitFilter(
        ILoginRateLimiter rateLimiter,
        ILogger<LoginRateLimitFilter> logger)
    {
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        var ipAddress =
            httpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            _logger.LogWarning(
                "Unable to determine client IP for login rate limiting.");

            context.Result = new StatusCodeResult(
                StatusCodes.Status503ServiceUnavailable);

            return;
        }

        RateLimitResult result;

        try
        {
            result = await _rateLimiter.CheckAsync(
                ipAddress,
                httpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Redis login rate limiter failed for IP {IpAddress}.",
                ipAddress);

            context.Result = new StatusCodeResult(
                StatusCodes.Status503ServiceUnavailable);

            return;
        }

        if (!result.IsAllowed)
        {
            httpContext.Response.Headers.RetryAfter =
                result.RetryAfterSeconds.ToString();

            context.Result = new ObjectResult(
                new ApiError(
                    "RATE_LIMIT_EXCEEDED",
                    "Too many login attempts. Please try again later."
                ))
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };

            return;
        }

        await next();
    }
}