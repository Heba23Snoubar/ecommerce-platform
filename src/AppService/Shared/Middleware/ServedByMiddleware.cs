using Microsoft.AspNetCore.Http;

namespace Shared.Middleware;

public class ServedByMiddleware
{
    private readonly RequestDelegate _next;

    public ServedByMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx)
    {
        ctx.Response.Headers["X-Served-By"] = Environment.MachineName;

  

        await _next(ctx);
    }
}