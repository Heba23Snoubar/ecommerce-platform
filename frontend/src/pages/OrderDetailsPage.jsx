import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, CheckCircle2, PackageCheck, Radio, RefreshCw } from "lucide-react";
import { storeApi } from "../services/storeApi";
import { useAuth } from "../context/AuthContext";
import { subscribeToOrder } from "../services/realtime";
import StatusBadge from "../components/StatusBadge";

export default function OrderDetailsPage() {
  const { id } = useParams();
  const { isAdmin } = useAuth();
  const [order, setOrder] = useState(null);
  const [error, setError] = useState("");
  const [statusNotice, setStatusNotice] = useState(null);
  const [updatingStatus, setUpdatingStatus] = useState(false);
  const [connectionState, setConnectionState] = useState("disconnected");

  useEffect(() => {
    storeApi
      .getOrder(id)
      .then(setOrder)
      .catch((e) =>
        setError(
          e.status === 404 ? "This order could not be found." : e.message
        )
      );
  }, [id]);

  // Realtime subscription for this specific order (Issue #49 / D3)
  useEffect(() => {
    if (!id) return;

    const unsubscribe = subscribeToOrder(id, {
      onStatusChanged: (event) => {
        // Update order status dynamically without page reload (D3-1)
        if (
          event.orderId &&
          String(event.orderId).toLowerCase() === String(id).toLowerCase()
        ) {
          setOrder((prev) => (prev ? { ...prev, status: event.status } : prev));
          setStatusNotice({
            status: event.status,
            time: new Date(event.occurredAtUtc || Date.now()).toLocaleTimeString()
          });

          // Auto-hide notice after 7 seconds
          setTimeout(() => {
            setStatusNotice(null);
          }, 7000);
        }
      },
      onConnectionChange: (current) => {
        setConnectionState(current);
      },
      onError: (err) => {
        console.warn(`[OrderDetailsPage] Realtime subscription error:`, err);
      }
    });

    return () => {
      // Cleanup on unmount (D3-2)
      unsubscribe();
    };
  }, [id]);

  // Admin status update action: PATCH /api/v1/orders/{id}/status (D3-3)
  const handleAdminStatusChange = async (newStatus) => {
    if (!order || order.status === newStatus) return;
    setUpdatingStatus(true);
    try {
      const updated = await storeApi.updateOrderStatus(order.id, newStatus);
      setOrder(updated);
      setStatusNotice({
        status: newStatus,
        time: new Date().toLocaleTimeString(),
        manual: true
      });
    } catch (err) {
      setError(err.message);
    } finally {
      setUpdatingStatus(false);
    }
  };

  if (error) {
    return (
      <div className="rounded-xl bg-rose-50 p-4 text-sm text-rose-600">
        {error}
      </div>
    );
  }

  if (!order) {
    return <div className="panel h-64 animate-pulse" />;
  }

  return (
    <div>
      <Link
        to="/orders"
        className="mb-6 inline-flex items-center gap-2 text-sm font-bold text-slate-500 hover:text-slate-800"
      >
        <ArrowLeft size={16} />
        Back to orders
      </Link>

      {/* Live Status Change Alert Toast (D3-1) */}
      {statusNotice && (
        <div className="mb-6 flex items-center justify-between gap-4 rounded-2xl border border-emerald-200 bg-emerald-50/90 p-4 text-emerald-950 shadow-md backdrop-blur">
          <div className="flex items-center gap-3">
            <div className="grid h-10 w-10 place-items-center rounded-xl bg-emerald-600 text-white shadow-sm">
              <CheckCircle2 size={20} className="animate-pulse" />
            </div>
            <div>
              <div className="text-xs font-extrabold uppercase tracking-wider text-emerald-700">
                {statusNotice.manual
                  ? "Status Updated Successfully"
                  : "Live Status Update Received!"}
              </div>
              <div className="text-sm font-semibold">
                Order status is now:{" "}
                <span className="font-extrabold text-emerald-900">
                  {statusNotice.status}
                </span>{" "}
                <span className="text-xs text-emerald-600">
                  ({statusNotice.time})
                </span>
              </div>
            </div>
          </div>
          <button
            onClick={() => setStatusNotice(null)}
            className="rounded-lg px-3 py-1.5 text-xs font-bold text-emerald-700 hover:bg-emerald-100/70"
          >
            Dismiss
          </button>
        </div>
      )}

      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2.5">
            <p className="text-xs font-extrabold uppercase tracking-widest text-indigo-500">
              Order details
            </p>

            {/* Realtime Live Tracking Indicator */}
            {connectionState === "connected" ? (
              <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-bold text-emerald-700">
                <span className="h-1.5 w-1.5 animate-ping rounded-full bg-emerald-500" />
                Live Tracking
              </span>
            ) : connectionState === "connecting" ? (
              <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-[10px] font-bold text-amber-700">
                <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-amber-500" />
                Connecting...
              </span>
            ) : (
              <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-bold text-slate-500">
                <Radio size={10} />
                Offline
              </span>
            )}
          </div>

          <h1 className="mt-2 font-display text-3xl font-extrabold text-slate-900">
            #{String(order.id).slice(0, 8)}
          </h1>
          <p className="mt-2 text-sm text-slate-500">
            Placed on {new Date(order.createdAt).toLocaleString()}
          </p>
        </div>

        <div className="flex items-center gap-3">
          <StatusBadge status={order.status} />
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_340px]">
        {/* Left Column: Items */}
        <div className="panel p-6">
          <h2 className="mb-5 font-display text-lg font-extrabold">
            Items in this order
          </h2>
          <div className="divide-y divide-slate-100">
            {order.items.map((i, index) => (
              <div
                key={index}
                className="flex items-center justify-between gap-4 py-4 first:pt-0"
              >
                <div>
                  <div className="text-sm font-bold text-slate-800">
                    {i.name}
                  </div>
                  <div className="mt-1 text-xs text-slate-400">
                    Qty: {i.quantity} × ${Number(i.unitPrice).toFixed(2)}
                  </div>
                </div>
                <div className="text-sm font-extrabold">
                  ${(i.quantity * i.unitPrice).toFixed(2)}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Right Column: Summary & Admin Controls */}
        <div className="space-y-6">
          <div className="panel h-fit p-6">
            <div className="mb-4 flex items-center gap-2 text-indigo-600">
              <PackageCheck size={19} />
              <h2 className="font-display font-extrabold text-slate-800">
                Summary
              </h2>
            </div>

            {isAdmin && (
              <div className="mb-4 text-sm text-slate-500">
                Customer:{" "}
                <span className="font-bold text-slate-700">
                  {order.customerName}
                </span>
              </div>
            )}

            <div className="flex justify-between border-t border-slate-100 pt-4">
              <span className="text-sm font-bold text-slate-600">
                Order total
              </span>
              <span className="font-display text-xl font-extrabold">
                ${Number(order.total).toFixed(2)}
              </span>
            </div>
          </div>

          {/* Admin Status Management Panel (D3-3) */}
          {isAdmin && (
            <div className="panel border-indigo-100 bg-gradient-to-br from-indigo-50/40 to-white p-6 shadow-sm">
              <h3 className="mb-2 text-xs font-extrabold uppercase tracking-wider text-indigo-700">
                Admin Controls
              </h3>
              <p className="mb-4 text-xs text-slate-500">
                Change order status to broadcast live updates to the customer's
                browser.
              </p>

              <div className="space-y-3">
                <label className="text-xs font-bold text-slate-700">
                  Order Status
                </label>
                <div className="flex items-center gap-2">
                  <select
                    value={order.status}
                    disabled={updatingStatus}
                    onChange={(e) => handleAdminStatusChange(e.target.value)}
                    className="w-full rounded-xl border border-slate-200 bg-white p-2.5 text-xs font-semibold text-slate-800 shadow-sm focus:border-indigo-500 focus:outline-none disabled:opacity-50"
                  >
                    <option value="Pending">Pending</option>
                    <option value="Confirmed">Confirmed</option>
                    <option value="Shipped">Shipped</option>
                    <option value="Delivered">Delivered</option>
                    <option value="Cancelled">Cancelled</option>
                  </select>
                  {updatingStatus && (
                    <RefreshCw
                      size={16}
                      className="animate-spin text-indigo-600"
                    />
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
