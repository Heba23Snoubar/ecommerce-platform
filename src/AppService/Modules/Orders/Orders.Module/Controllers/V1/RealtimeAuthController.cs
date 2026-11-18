using System.Security.Claims;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Orders.Contracts;
using Orders.Module.Messaging;
using PusherServer;
using Shared.Errors;

namespace Orders.Module.Controllers.V1;

public sealed record RealtimeAuthRequest(
    [property: JsonPropertyName("socket_id")] string? SocketId,
    [property: JsonPropertyName("channel_name")] string? ChannelName);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/realtime")]
[Authorize]
public class RealtimeAuthController : ControllerBase
{
    private const string AdminOrdersChannel = "private-admin-orders";
    private const string OrderChannelPrefix = "private-order.";

    private readonly IOrdersApi _orders;
    private readonly Pusher _pusher;

    public RealtimeAuthController(IOrdersApi orders, IConfiguration config)
    {
        _orders = orders;
        var (appId, appKey, appSecret, options) = PusherConfig.Create(config);
        _pusher = new Pusher(appId, appKey, appSecret, options);
    }

    [HttpPost("auth")]
    public async Task<IActionResult> AuthenticateChannel(CancellationToken ct)
    {
        var (socketId, channelName) = await ReadCredentialsAsync(ct);

        if (string.IsNullOrWhiteSpace(socketId) || string.IsNullOrWhiteSpace(channelName))
            throw new AppException(422, "validation_failed", "socket_id and channel_name are required.");

        // C3-2: قناة المسؤول — السماح فقط لدور Admin
        if (channelName == AdminOrdersChannel)
        {
            if (!User.IsInRole("Admin"))
                return ForbidOr403("Only admins can subscribe to private-admin-orders.");
        }
        // C3-3: قناة الطلب — التحقق من الملكية عبر IOrdersApi.GetAsync
        else if (channelName.StartsWith(OrderChannelPrefix, StringComparison.Ordinal))
        {
            var rawId = channelName[OrderChannelPrefix.Length..];
            if (!Guid.TryParse(rawId, out var orderId))
                return ForbidOr403("Invalid order channel format.");

            try
            {
                await _orders.GetAsync(orderId, CurrentUserId, User.IsInRole("Admin"), ct);
            }
            catch (AppException ex) when (ex.StatusCode is 403 or 404)
            {
                return ForbidOr403("You are not authorized to subscribe to this order channel.");
            }
        }
        else
        {
            // أي قناة أخرى غير معروفة تُرفض بـ 403
            return ForbidOr403("Unknown or unauthorized channel.");
        }

        // C3-4: توليد التوقيع بحزمة Pusher
        var authData = _pusher.Authenticate(channelName, socketId).ToJson();
        return Content(authData, "application/json");
    }

    private async Task<(string? SocketId, string? ChannelName)> ReadCredentialsAsync(CancellationToken ct)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            return (form["socket_id"].FirstOrDefault(), form["channel_name"].FirstOrDefault());
        }

        if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var body = await Request.ReadFromJsonAsync<RealtimeAuthRequest>(cancellationToken: ct);
            return (body?.SocketId, body?.ChannelName);
        }

        return (Request.Query["socket_id"].FirstOrDefault(), Request.Query["channel_name"].FirstOrDefault());
    }

    private ObjectResult ForbidOr403(string message) =>
        StatusCode(StatusCodes.Status403Forbidden, new ApiError("forbidden_channel", message));

    private Guid CurrentUserId
    {
        get
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(raw, out var id)
                ? id
                : throw new AppException(401, "invalid_token", "User id claim is missing or invalid.");
        }
    }
}