import { describe, it, beforeEach } from "node:test";
import assert from "node:assert/strict";

// Setup mock window & localStorage before importing app modules
const storageStore = new Map();
global.window = {
  location: { assign: () => {} }
};
global.localStorage = {
  getItem: (key) => storageStore.get(key) ?? null,
  setItem: (key, val) => storageStore.set(key, String(val)),
  removeItem: (key) => storageStore.delete(key),
  clear: () => storageStore.clear()
};

// Import unit under test
import {
  normalizeRealtimeOrder,
  getRealtimeClient,
  disconnectRealtime,
  subscribeToAdminOrders,
  subscribeToOrder,
  getRealtimeConnectionState,
  getAuthHeaders
} from "../src/services/realtime.js";
import { storage } from "../src/utils/storage.js";

describe("Realtime Service & Contract Validation (REQ4)", () => {
  beforeEach(() => {
    storageStore.clear();
    disconnectRealtime();
  });

  describe("Issue #45 / D1: Setup, Auth & Payloads", () => {
    it("D1-1: initializes Pusher client with correct default WebSocket options", () => {
      const client = getRealtimeClient();
      assert.ok(client, "Pusher client instance should be created");
      assert.equal(client.key, "app-key");
      assert.equal(client.config.wsHost, "localhost");
      assert.equal(client.config.wsPort, 6001);
      assert.equal(client.config.useTLS, false);
      assert.equal(typeof client.config.channelAuthorizer, "function");
    });

    it("D1-2: dynamically attaches JWT bearer token to auth headers", () => {
      // Case A: No token stored -> empty headers
      const headersNoToken = getAuthHeaders();
      assert.deepEqual(headersNoToken, {});

      // Case B: User logged in with JWT
      const testToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test-payload";
      storage.set("shopflow_token", testToken);

      const headersWithToken = getAuthHeaders();
      assert.equal(headersWithToken.Authorization, `Bearer ${testToken}`);
    });

    it("D1-1: normalizeRealtimeOrder handles standard camelCase OrderDto", () => {
      const payload = {
        id: "b3a1e9c4-52d7-4b7e-8c1a-2f6d9a41c7e5",
        userId: "9d2f7a58-1c3e-4a66-b0f4-5e8d3c2a1b97",
        customerName: "Khadija",
        status: "Pending",
        total: 150.0,
        createdAt: "2026-11-14T10:00:00Z",
        items: [
          {
            productId: "4c7e1a02-8b15-4d3f-a9e6-7f2b5c8d0e31",
            productName: "Keyboard",
            unitPrice: 50.0,
            quantity: 2
          },
          {
            productId: "e8a3d6b1-2f49-4c70-95a8-1d7e0b3c6f52",
            productName: "Mouse",
            unitPrice: 50.0,
            quantity: 1
          }
        ]
      };

      const normalized = normalizeRealtimeOrder(payload);
      assert.equal(normalized.id, payload.id);
      assert.equal(normalized.customerName, "Khadija");
      assert.equal(normalized.status, "Pending");
      assert.equal(normalized.total, 150.0);
      assert.equal(normalized.itemCount, 3);
      assert.equal(normalized.items.length, 2);
    });

    it("D1-1: normalizeRealtimeOrder handles PascalCase ASP.NET DTO", () => {
      const payload = {
        Id: "11111111-2222-3333-4444-555555555555",
        UserId: "66666666-7777-8888-9999-000000000000",
        Status: "Confirmed",
        Total: 99.99,
        CreatedAt: "2026-11-15T12:00:00Z",
        Items: [
          {
            ProductId: "p1",
            ProductName: "Monitor",
            UnitPrice: 99.99,
            Quantity: 1
          }
        ]
      };

      const normalized = normalizeRealtimeOrder(payload);
      assert.equal(normalized.id, payload.Id);
      assert.equal(normalized.status, "Confirmed");
      assert.equal(normalized.total, 99.99);
      assert.equal(normalized.itemCount, 1);
      assert.equal(normalized.customerName, "Customer 66666666");
    });

    it("disconnectRealtime cleans up connection and resets instance", () => {
      const client = getRealtimeClient();
      assert.ok(client);
      disconnectRealtime();
      assert.equal(getRealtimeConnectionState(), "disconnected");
    });
  });

  describe("Issue #47 / D2: Admin Orders Realtime Subscription", () => {
    it("D2-2: subscribes to private-admin-orders and delivers order.created", () => {
      const received = [];
      const unsubscribe = subscribeToAdminOrders({
        onOrderCreated: (order) => received.push(order)
      });

      const client = getRealtimeClient();
      const channel = client.channel("private-admin-orders");
      assert.ok(channel, "Channel private-admin-orders should be subscribed");

      const mockOrder = {
        id: "order-123",
        userId: "user-abc",
        status: "Pending",
        total: 45.0,
        items: [{ productId: "p1", productName: "Item", unitPrice: 45.0, quantity: 1 }]
      };

      channel.emit("order.created", mockOrder);

      assert.equal(received.length, 1);
      assert.equal(received[0].id, "order-123");
      assert.equal(received[0].total, 45.0);

      // Clean up
      unsubscribe();
    });

    it("D2-3: unbinds event listeners and unsubscribes channel on unmount", () => {
      const unsubscribe = subscribeToAdminOrders({
        onOrderCreated: () => {}
      });

      const client = getRealtimeClient();
      assert.ok(client.channel("private-admin-orders"));

      unsubscribe();
      // Channel should be unsubscribed
      assert.equal(client.channel("private-admin-orders"), undefined);
    });
  });

  describe("Issue #49 / D3: Order Live Tracking", () => {
    it("D3-1: subscribes to private-order.{id} and receives order.status-changed", () => {
      const orderId = "c8f1a4e2-9b3d-4e5a-8f7c-1d2e3f4a5b6c";
      const statusUpdates = [];

      const unsubscribe = subscribeToOrder(orderId, {
        onStatusChanged: (event) => statusUpdates.push(event)
      });

      const client = getRealtimeClient();
      const channel = client.channel(`private-order.${orderId}`);
      assert.ok(channel, `Channel private-order.${orderId} should exist`);

      channel.emit("order.status-changed", {
        orderId,
        status: "Shipped",
        occurredAtUtc: "2026-11-14T10:15:00Z"
      });

      assert.equal(statusUpdates.length, 1);
      assert.equal(statusUpdates[0].orderId, orderId);
      assert.equal(statusUpdates[0].status, "Shipped");

      unsubscribe();
      assert.equal(client.channel(`private-order.${orderId}`), undefined);
    });
  });

  describe("Issue #53 / E1: Security & Edge Cases", () => {
    it("E1-4: reports pusher:subscription_error when server rejects auth (403)", () => {
      const errors = [];
      const unsubscribe = subscribeToAdminOrders({
        onError: (err) => errors.push(err)
      });

      const client = getRealtimeClient();
      const channel = client.channel("private-admin-orders");

      const authError = {
        type: "AuthError",
        error: "403 Forbidden - Admin role required",
        status: 403
      };

      channel.emit("pusher:subscription_error", authError);

      assert.equal(errors.length, 1);
      assert.equal(errors[0].status, 403);

      unsubscribe();
    });
  });
});
