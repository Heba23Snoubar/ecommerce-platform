import Pusher from "pusher-js";
import { storage } from "../utils/storage.js";

const env =
  typeof import.meta !== "undefined" && import.meta.env
    ? import.meta.env
    : typeof process !== "undefined" && process.env
    ? process.env
    : {};

const PUSHER_KEY = env.VITE_PUSHER_KEY || "app-key";
const PUSHER_HOST = env.VITE_PUSHER_HOST || "localhost";
const PUSHER_PORT = Number(env.VITE_PUSHER_PORT) || 6001;
const PUSHER_CLUSTER = env.VITE_PUSHER_CLUSTER || "mt1";
const PUSHER_AUTH_ENDPOINT =
  env.VITE_PUSHER_AUTH_ENDPOINT || "/api/v1/realtime/auth";

let pusherInstance = null;

/**
 * Normalizes incoming order payload from WebSocket to match frontend internal order structure.
 * Supports both OrderDto shapes from ASP.NET (camelCase and PascalCase).
 */
export function normalizeRealtimeOrder(raw) {
  if (!raw) return null;
  const o = typeof raw === "string" ? JSON.parse(raw) : raw;

  const rawItems = Array.isArray(o?.items ?? o?.Items)
    ? o.items ?? o.Items
    : [];

  const items = rawItems.map((i) => ({
    productId: i?.productId ?? i?.ProductId,
    name: i?.productName ?? i?.ProductName ?? i?.name ?? i?.Name ?? "Item",
    quantity: Number(i?.quantity ?? i?.Quantity ?? 1),
    unitPrice: Number(i?.unitPrice ?? i?.UnitPrice ?? 0)
  }));

  const calculatedTotal = items.reduce(
    (sum, i) => sum + i.unitPrice * i.quantity,
    0
  );
  const calculatedCount = items.reduce((sum, i) => sum + i.quantity, 0);

  const userId = o?.userId ?? o?.UserId;
  return {
    id: o?.id ?? o?.Id,
    userId,
    customerName:
      o?.customerName ??
      o?.CustomerName ??
      (userId ? `Customer ${String(userId).slice(0, 8)}` : "Customer"),
    status: o?.status ?? o?.Status ?? "Pending",
    total: Number(o?.total ?? o?.Total ?? calculatedTotal),
    createdAt: o?.createdAt ?? o?.CreatedAt ?? new Date().toISOString(),
    itemCount: Number(
      o?.itemCount ?? o?.ItemCount ?? (calculatedCount > 0 ? calculatedCount : 1)
    ),
    items
  };
}

/**
 * Returns dynamic headers for channel authorization containing JWT bearer token.
 */
export function getAuthHeaders() {
  const token = storage.get("shopflow_token");
  return token ? { Authorization: `Bearer ${token}` } : {};
}

/**
 * Returns or initializes the shared Pusher client instance.
 * Automatically attaches the JWT bearer token from localStorage for private channel authorization.
 */
export function getRealtimeClient() {
  if (pusherInstance) return pusherInstance;

  pusherInstance = new Pusher(PUSHER_KEY, {
    wsHost: PUSHER_HOST,
    wsPort: PUSHER_PORT,
    wssPort: PUSHER_PORT,
    forceTLS: false,
    disableStats: true,
    enabledTransports: ["ws", "wss"],
    cluster: PUSHER_CLUSTER,
    channelAuthorization: {
      endpoint: PUSHER_AUTH_ENDPOINT,
      transport: "ajax",
      headersProvider: getAuthHeaders
    },
    // Fallback support for older Pusher options
    authEndpoint: PUSHER_AUTH_ENDPOINT,
    auth: {
      transport: "ajax",
      headersProvider: getAuthHeaders
    }
  });

  return pusherInstance;
}

/**
 * Disconnects the Pusher client and clears cached instance.
 * Useful on logout or when switching accounts.
 */
export function disconnectRealtime() {
  if (pusherInstance) {
    try {
      pusherInstance.disconnect();
    } catch {
      // Ignore disconnect errors during teardown
    }
    pusherInstance = null;
  }
}

/**
 * Returns current connection state string (e.g. 'connected', 'connecting', 'disconnected', 'unavailable').
 */
export function getRealtimeConnectionState() {
  return pusherInstance?.connection?.state || "disconnected";
}

/**
 * Subscribes to the admin orders channel (private-admin-orders) to receive live order.created events.
 *
 * @param {Object} options
 * @param {Function} options.onOrderCreated - Callback receiving normalized OrderDto
 * @param {Function} options.onConnectionChange - Callback receiving (currentState, previousState)
 * @param {Function} options.onError - Callback receiving channel subscription errors
 * @returns {Function} Unsubscribe function to clean up listeners and channel on component unmount
 */
export function subscribeToAdminOrders({
  onOrderCreated,
  onConnectionChange,
  onError
} = {}) {
  const pusher = getRealtimeClient();
  const channelName = "private-admin-orders";
  const channel = pusher.subscribe(channelName);

  const orderCreatedHandler = (data) => {
    try {
      const order = normalizeRealtimeOrder(data);
      if (order && onOrderCreated) {
        onOrderCreated(order);
      }
    } catch (err) {
      console.error("[Realtime] Failed to parse order.created event:", err);
    }
  };

  const errorHandler = (err) => {
    console.warn("[Realtime] Subscription error on private-admin-orders:", err);
    if (onError) onError(err);
  };

  channel.bind("order.created", orderCreatedHandler);
  channel.bind("pusher:subscription_error", errorHandler);

  let stateChangeHandler = null;
  if (onConnectionChange) {
    stateChangeHandler = (states) => {
      onConnectionChange(states.current, states.previous);
    };
    pusher.connection.bind("state_change", stateChangeHandler);
    onConnectionChange(pusher.connection.state, null);
  }

  return () => {
    try {
      channel.unbind("order.created", orderCreatedHandler);
      channel.unbind("pusher:subscription_error", errorHandler);
      pusher.unsubscribe(channelName);
      if (stateChangeHandler) {
        pusher.connection.unbind("state_change", stateChangeHandler);
      }
    } catch {
      // Ignore unbind errors during unmount
    }
  };
}

/**
 * Subscribes to an individual customer's order channel (private-order.{orderId})
 * to receive live order.status-changed events.
 *
 * @param {string|Guid} orderId - Target order ID
 * @param {Object} options
 * @param {Function} options.onStatusChanged - Callback receiving { orderId, status, occurredAtUtc }
 * @param {Function} options.onConnectionChange - Callback receiving (currentState, previousState)
 * @param {Function} options.onError - Callback receiving channel subscription errors
 * @returns {Function} Unsubscribe function to clean up listeners and channel on component unmount
 */
export function subscribeToOrder(
  orderId,
  { onStatusChanged, onConnectionChange, onError } = {}
) {
  if (!orderId) return () => {};

  const pusher = getRealtimeClient();
  const channelName = `private-order.${orderId}`;
  const channel = pusher.subscribe(channelName);

  const statusChangedHandler = (data) => {
    try {
      const payload = typeof data === "string" ? JSON.parse(data) : data;
      const statusData = {
        orderId: payload?.orderId ?? payload?.OrderId ?? orderId,
        status: payload?.status ?? payload?.Status,
        occurredAtUtc:
          payload?.occurredAtUtc ??
          payload?.OccurredAtUtc ??
          new Date().toISOString()
      };
      if (onStatusChanged) {
        onStatusChanged(statusData);
      }
    } catch (err) {
      console.error(
        "[Realtime] Failed to parse order.status-changed event:",
        err
      );
    }
  };

  const errorHandler = (err) => {
    console.warn(
      `[Realtime] Subscription error on private-order.${orderId}:`,
      err
    );
    if (onError) onError(err);
  };

  channel.bind("order.status-changed", statusChangedHandler);
  channel.bind("pusher:subscription_error", errorHandler);

  let stateChangeHandler = null;
  if (onConnectionChange) {
    stateChangeHandler = (states) => {
      onConnectionChange(states.current, states.previous);
    };
    pusher.connection.bind("state_change", stateChangeHandler);
    onConnectionChange(pusher.connection.state, null);
  }

  return () => {
    try {
      channel.unbind("order.status-changed", statusChangedHandler);
      channel.unbind("pusher:subscription_error", errorHandler);
      pusher.unsubscribe(channelName);
      if (stateChangeHandler) {
        pusher.connection.unbind("state_change", stateChangeHandler);
      }
    } catch {
      // Ignore unbind errors during unmount
    }
  };
}
