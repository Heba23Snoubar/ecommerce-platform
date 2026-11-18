using Microsoft.Extensions.Configuration;
using PusherServer;

namespace Orders.Module.Messaging;

internal static class PusherConfig
{
    public static (string AppId, string AppKey, string AppSecret, PusherOptions Options) Create(IConfiguration config)
    {
        var appId = config["PUSHER_APP_ID"] ?? config["Pusher:AppId"] ?? "app-id";
        var appKey = config["PUSHER_APP_KEY"] ?? config["Pusher:Key"] ?? "app-key";
        var appSecret = config["PUSHER_APP_SECRET"] ?? config["Pusher:Secret"] ?? "change_me_pusher";
        var host = config["PUSHER_HOST"] ?? config["Pusher:Host"] ?? "localhost";
        var port = int.TryParse(config["PUSHER_PORT"] ?? config["Pusher:Port"], out var p) ? p : 6001;

        var options = new PusherOptions
        {
            HostName = $"{host}:{port}",
            Encrypted = false
        };

        return (appId, appKey, appSecret, options);
    }
}