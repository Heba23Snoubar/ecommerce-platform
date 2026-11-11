using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;



namespace Shared.Database
{
    
    public sealed class ReadYourOwnWritesMiddleware
    {
        private readonly RequestDelegate _next;

        public ReadYourOwnWritesMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IDatabaseRoutingContext routingContext)
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                routingContext.MarkWriteOperation();
            }

            await _next(context);
        }
    }
}
