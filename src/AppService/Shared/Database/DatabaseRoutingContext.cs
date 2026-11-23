using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Shared.Database
{


 
    public sealed class DatabaseRoutingContext : IDatabaseRoutingContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan WriteStickyDuration = TimeSpan.FromSeconds(5);

        public DatabaseRoutingContext(
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache cache)
        {
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
        }

        public bool ShouldUsePrimaryDatabase()
        {
            var context = _httpContextAccessor.HttpContext;

            // Requests outside HTTP must use the primary database.
            if (context is null)
            {
                return true;
            }

            var method = context.Request.Method;

            // Mutating requests always use the primary database.
            if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            {
                return true;
            }

             return context.Items.TryGetValue(
                       ReadYourOwnWritesMiddleware.UsePrimaryDatabaseItemKey,
                       out var value)
                   && value is true;
        }

        public void MarkWriteOperation()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null)
            {
                return;
            }

             context.Items[ReadYourOwnWritesMiddleware.UsePrimaryDatabaseItemKey] = true;
        }
        public string ResolveConnectionString(string primaryConnectionString, string? replicaConnectionString = null)
        {
            var usePrimary = ShouldUsePrimaryDatabase();
            var method = _httpContextAccessor.HttpContext?.Request.Method ?? "SYSTEM";
            var path = _httpContextAccessor.HttpContext?.Request.Path.Value ?? "";

            if (usePrimary || !IsReplicaHealthy())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[DB-ROUTING] {method} {path} --> PRIMARY DB (Port 5432)");
                Console.ResetColor();
                return primaryConnectionString;
            }


            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[DB-ROUTING] {method} {path} --> READ REPLICA DB (app-db-replica:5432)");
            Console.ResetColor();

            if (!string.IsNullOrWhiteSpace(replicaConnectionString))
            {
            return replicaConnectionString;
            }

           return primaryConnectionString.Replace("Host=app-db", "Host=app-db-replica");
        }


        private bool IsReplicaHealthy()
        {
            return _cache.GetOrCreate("replica_health_status", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);

                try
                {
                    using var client = new System.Net.Sockets.TcpClient();

                    var result = client.BeginConnect(
                        "app-db-replica",
                        5432,
                        null,
                        null);

                    var connected = result.AsyncWaitHandle.WaitOne(
                        TimeSpan.FromMilliseconds(500));

                    if (!connected)
                    {
                        return false;
                    }

                    client.EndConnect(result);
                    return client.Connected;
                }
                catch
                {
                    return false;
                }
            });
        }
         
        private static IEnumerable<string> GetClientKeys(HttpContext context)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? context.User.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(userId))
            {
                yield return $"recent_write:user:{userId}";
            }

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "local";
            yield return $"recent_write:ip:{ip}";
        }
    }

}
