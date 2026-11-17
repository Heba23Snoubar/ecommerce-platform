import { useEffect, useState, useCallback } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, Bell, Radio, X } from "lucide-react";
import { storeApi } from "../services/storeApi";
import { useAuth } from "../context/AuthContext";
import { subscribeToAdminOrders } from "../services/realtime";
import PageHeading from "../components/PageHeading";
import EmptyState from "../components/EmptyState";
import StatusBadge from "../components/StatusBadge";

export default function OrdersPage() {
  const { user, isAdmin } = useAuth();
  const [orders, setOrders] = useState([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [connectionState, setConnectionState] = useState("disconnected");
  const [newOrderToast, setNewOrderToast] = useState(null);
  const [highlightedOrderId, setHighlightedOrderId] = useState(null);

  const load = useCallback(
    () =>
      storeApi
        .getOrders(user, isAdmin)
        .then(setOrders)
        .catch((e) => setError(e.message))
        .finally(() => setBusy(false)),
    [user, isAdmin]
  );

  useEffect(() => {
    load();
  }, [load]);

  // Realtime subscription for Admins (Issue #47 / D2)
  useEffect(() => {
    if (!isAdmin) return;

    const unsubscribe = subscribeToAdminOrders({
      onOrderCreated: (newOrder) => {
        // Prepend new order without refresh, avoiding duplicates (D2-2)
        setOrders((prev) => {
          if (prev.some((o) => o.id === newOrder.id)) return prev;
          return [newOrder, ...prev];
        });

        // Visual highlight on row and toast notification (D2-3)
        setHighlightedOrderId(newOrder.id);
        setNewOrderToast({
          id: newOrder.id,
          customerName: newOrder.customerName,
          total: newOrder.total,
          itemCount: newOrder.itemCount
        });

        setTimeout(() => {
          setHighlightedOrderId((curr) =>
            curr === newOrder.id ? null : curr
          );
        }, 5000);
      },
      onConnectionChange: (current) => {
        setConnectionState(current);
      },
      onError: (err) => {
        console.warn("[OrdersPage] Realtime subscription error:", err);
      }
    });

    return () => {
      // Cleanup on unmount (D2-3)
      unsubscribe();
    };
  }, [isAdmin]);

  const changeStatus = async (id, status) => {
    try {
      await storeApi.updateOrderStatus(id, status);
      load();
    } catch (e) {
      setError(e.message);
    }
  };

  return (
    <div>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <PageHeading
          eyebrow="Order management"
          title={isAdmin ? "All orders" : "My orders"}
          description={
            isAdmin
              ? "Review customer orders and keep their status up to date."
              : "Track your purchases and check the latest order status."
          }
        />

        {/* Realtime Connection Indicator for Admins (D2-4) */}
        {isAdmin && (
          <div className="flex items-center gap-2">
            {connectionState === "connected" ? (
              <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-bold text-emerald-700 shadow-sm">
                <span className="relative flex h-2 w-2">
                  <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75"></span>
                  <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500"></span>
                </span>
                Live Connected
              </span>
            ) : connectionState === "connecting" ? (
              <span className="inline-flex items-center gap-1.5 rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-xs font-bold text-amber-700">
                <span className="h-2 w-2 animate-pulse rounded-full bg-amber-500" />
                Connecting...
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-100 px-3 py-1 text-xs font-bold text-slate-500">
                <Radio size={12} />
                Realtime Offline
              </span>
            )}
          </div>
        )}
      </div>

      {/* Visual Toast Notification for New Incoming Orders (D2-3) */}
      {newOrderToast && (
        <div className="mb-5 flex items-center justify-between gap-4 rounded-2xl border border-indigo-100 bg-indigo-50/90 p-4 text-indigo-950 shadow-md backdrop-blur">
          <div className="flex items-center gap-3">
            <div className="grid h-10 w-10 place-items-center rounded-xl bg-indigo-600 text-white shadow-sm">
              <Bell size={20} className="animate-bounce" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-extrabold uppercase tracking-wider text-indigo-600">
                  New Order Received!
                </span>
                <span className="rounded-md bg-indigo-200/60 px-1.5 py-0.5 text-[10px] font-bold text-indigo-800">
                  Just now
                </span>
              </div>
              <div className="text-sm font-semibold">
                Order #{String(newOrderToast.id).slice(0, 8)} • $
                {Number(newOrderToast.total).toFixed(2)} from{" "}
                <span className="font-bold">{newOrderToast.customerName}</span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Link
              to={`/orders/${newOrderToast.id}`}
              className="inline-flex items-center gap-1 rounded-xl bg-indigo-600 px-3.5 py-2 text-xs font-bold text-white shadow hover:bg-indigo-700"
            >
              View Order
              <ArrowUpRight size={14} />
            </Link>
            <button
              onClick={() => setNewOrderToast(null)}
              className="rounded-lg p-2 text-indigo-500 hover:bg-indigo-100/60 hover:text-indigo-800"
              title="Dismiss"
            >
              <X size={16} />
            </button>
          </div>
        </div>
      )}

      {error && (
        <p className="mb-4 rounded-xl bg-rose-50 p-3 text-sm text-rose-600">
          {error}
        </p>
      )}

      {busy ? (
        <div className="panel h-56 animate-pulse" />
      ) : orders.length === 0 ? (
        <EmptyState
          title="No orders yet"
          description="Orders you place will appear here."
        />
      ) : (
        <div className="panel overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[700px] text-left">
              <thead className="bg-slate-50 text-[10px] font-extrabold uppercase tracking-wider text-slate-400">
                <tr>
                  <th className="px-6 py-4">Order</th>
                  {isAdmin && <th className="px-6 py-4">Customer</th>}
                  <th className="px-6 py-4">Date</th>
                  <th className="px-6 py-4">Items</th>
                  <th className="px-6 py-4">Total</th>
                  <th className="px-6 py-4">Status</th>
                  <th className="px-6 py-4">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-sm">
                {orders.map((o) => {
                  const isHighlighted = o.id === highlightedOrderId;
                  return (
                    <tr
                      key={o.id}
                      className={`transition-colors duration-700 ${
                        isHighlighted
                          ? "bg-indigo-50/80 font-medium"
                          : "hover:bg-slate-50/70"
                      }`}
                    >
                      <td className="px-6 py-4 font-extrabold text-slate-800">
                        <div className="flex items-center gap-2">
                          #{String(o.id).slice(0, 8)}
                          {isHighlighted && (
                            <span className="rounded bg-indigo-600 px-1.5 py-0.5 text-[9px] font-extrabold uppercase text-white">
                              New
                            </span>
                          )}
                        </div>
                      </td>
                      {isAdmin && (
                        <td className="px-6 py-4 text-slate-600">
                          {o.customerName}
                        </td>
                      )}
                      <td className="px-6 py-4 text-slate-500">
                        {new Date(o.createdAt).toLocaleDateString()}
                      </td>
                      <td className="px-6 py-4 text-slate-500">
                        {o.itemCount} items
                      </td>
                      <td className="px-6 py-4 font-bold">
                        ${Number(o.total).toFixed(2)}
                      </td>
                      <td className="px-6 py-4">
                        <StatusBadge status={o.status} />
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2">
                          <Link
                            to={`/orders/${o.id}`}
                            className="rounded-lg p-2 text-indigo-600 hover:bg-indigo-50"
                            title="View details"
                          >
                            <ArrowUpRight size={17} />
                          </Link>
                          {isAdmin && (
                            <select
                              className="max-w-32 rounded-lg border border-slate-200 p-2 text-xs font-semibold text-slate-700 focus:border-indigo-500 focus:outline-none"
                              value={o.status}
                              onChange={(e) =>
                                changeStatus(o.id, e.target.value)
                              }
                            >
                              <option>Pending</option>
                              <option>Confirmed</option>
                              <option>Shipped</option>
                              <option>Delivered</option>
                              <option>Cancelled</option>
                            </select>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
