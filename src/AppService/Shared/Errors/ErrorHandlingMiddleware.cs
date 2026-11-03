using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Errors;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _log;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (AppException ex) when (!ctx.Response.HasStarted)
        {
            await Write(ctx, ex.StatusCode, ex.Code, ex.Message, ex.Details);
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            _log.LogError(ex, "Unhandled exception");
            await Write(ctx, 500, "internal_error", "An unexpected error occurred.", null);
        }
    }

    private static Task Write(HttpContext ctx, int status, string code, string message, object? details)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { code, message, details });
    }
}