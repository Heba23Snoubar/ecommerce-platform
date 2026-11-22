using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Database
{
    public sealed class ReadYourOwnWritesMiddleware
    {
        public const string UsePrimaryDatabaseItemKey =
            "DatabaseRoutingContext.UsePrimaryDatabase";

        private readonly RequestDelegate _next;
        private readonly ILogger<ReadYourOwnWritesMiddleware> _logger;

        public ReadYourOwnWritesMiddleware(
            RequestDelegate next,
            ILogger<ReadYourOwnWritesMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IWriteRoutingMarkerStore markerStore)
        {
            var isRead =
                HttpMethods.IsGet(context.Request.Method) ||
                HttpMethods.IsHead(context.Request.Method);

            if (isRead)
            {
                try
                {
                    var hasRecentWrite =
                        await markerStore.HasRecentWriteAsync(
                            context.RequestAborted);

                    context.Items[UsePrimaryDatabaseItemKey] = hasRecentWrite;
                }
                catch (Exception ex)
                {
                    // Fail safe: if Redis cannot be checked, read from primary.
                    _logger.LogWarning(
                        ex,
                        "Could not check the recent-write marker; routing this request to the primary database.");

                    context.Items[UsePrimaryDatabaseItemKey] = true;
                }

                await _next(context);
                return;
            }

            await _next(context);

            // Start the five-second window only after a successful mutation.
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                try
                {
                    await markerStore.MarkWriteAsync(
                        context.RequestAborted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Could not store the recent-write marker after a successful write.");
                }
            }
        }
    }
}