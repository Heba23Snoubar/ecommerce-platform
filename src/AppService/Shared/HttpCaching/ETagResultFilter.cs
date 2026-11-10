using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Shared.HttpCaching;

public sealed class ETagResultFilter : IAsyncResultFilter
{
    private readonly JsonSerializerOptions _json;

    public ETagResultFilter(IOptions<JsonOptions> jsonOptions)
    {
        _json = jsonOptions.Value.JsonSerializerOptions;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var http = context.HttpContext;

        
        if (HttpMethods.IsGet(http.Request.Method) &&
            context.Result is ObjectResult { StatusCode: null or StatusCodes.Status200OK, Value: { } value })
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), _json);
            var hash = SHA256.HashData(body);
            var etag = new EntityTagHeaderValue($"\"{Convert.ToHexString(hash, 0, 16)}\"");

            var response = http.Response.GetTypedHeaders();
            response.ETag = etag;
            response.CacheControl = new CacheControlHeaderValue { NoCache = true };

            var candidates = http.Request.GetTypedHeaders().IfNoneMatch;
            var matched = candidates.Any(c =>
                c.Equals(EntityTagHeaderValue.Any) || c.Compare(etag, useStrongComparison: false));

            if (matched)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status304NotModified);
            }
        }

        await next();
    }
}