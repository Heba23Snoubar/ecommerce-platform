using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Contracts;
using Orders.Module.Ports;
using PusherServer;

namespace Orders.Module.Messaging;

internal sealed class PusherOrderRealtimeNotifier : IOrderRealtimeNotifier
{
    private const string AdminOrdersChannel = "private-admin-orders";
    private const string OrderCreatedEvent = "order.created";
    private const string OrderStatusChangedEvent = "order.status-changed";

    private readonly Pusher _pusher;
    private readonly ILogger<PusherOrderRealtimeNotifier> _log;

    public PusherOrderRealtimeNotifier(IConfiguration config, ILogger<PusherOrderRealtimeNotifier> log)
    {
        var (appId, appKey, appSecret, options) = PusherConfig.Create(config);
        _pusher = new Pusher(appId, appKey, appSecret, options);
        _log = log;
    }

    public async Task NotifyOrderCreatedAsync(OrderSummaryDto order, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var result = await _pusher.TriggerAsync(
            channelName: AdminOrdersChannel,
            eventName: OrderCreatedEvent,
            data: new
            {
                id = order.Id,
                userId = order.UserId,
                status = order.Status.ToString(),
                total = order.Total,
                createdAt = order.CreatedAt,
                itemCount = order.ItemCount
            });

        EnsureSuccess(result, AdminOrdersChannel, OrderCreatedEvent);

        _log.LogInformation("Broadcasted {Event} to {Channel} for order {OrderId}",
            OrderCreatedEvent, AdminOrdersChannel, order.Id);
    }

    public async Task NotifyOrderStatusChangedAsync(OrderStatusChangedRealtimePayload payload, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var channel = $"private-order.{payload.OrderId}";
        var result = await _pusher.TriggerAsync(
            channelName: channel,
            eventName: OrderStatusChangedEvent,
            data: new
            {
                orderId = payload.OrderId,
                oldStatus = payload.OldStatus.ToString(),
                newStatus = payload.NewStatus.ToString(),
                updatedAt = payload.UpdatedAt
            });

        EnsureSuccess(result, channel, OrderStatusChangedEvent);

        _log.LogInformation("Broadcasted {Event} to {Channel} ({OldStatus} -> {NewStatus})",
            OrderStatusChangedEvent, channel, payload.OldStatus, payload.NewStatus);
    }

    private static void EnsureSuccess(ITriggerResult result, string channel, string eventName)
    {
        if (result.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            throw new InvalidOperationException(
                $"Pusher trigger '{eventName}' on '{channel}' failed with HTTP {(int)result.StatusCode}: {result.Body}");
        }
    }
}