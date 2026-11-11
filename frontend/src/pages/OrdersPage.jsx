import { useEffect,useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight } from "lucide-react";
import { storeApi } from "../services/storeApi";
import { useAuth } from "../context/AuthContext";
import PageHeading from "../components/PageHeading";
import EmptyState from "../components/EmptyState";
import StatusBadge from "../components/StatusBadge";
export default function OrdersPage(){
  const {user,isAdmin}=useAuth(); const [orders,setOrders]=useState([]); const [error,setError]=useState(""); const [busy,setBusy]=useState(true);
  const load=()=>storeApi.getOrders(user,isAdmin).then(setOrders).catch(e=>setError(e.message)).finally(()=>setBusy(false));
  useEffect(()=>{load();},[user,isAdmin]);
  const changeStatus=async(id,status)=>{try{await storeApi.updateOrderStatus(id,status);load();}catch(e){setError(e.message);}};
  return <div><PageHeading eyebrow="Order management" title={isAdmin?"All orders":"My orders"} description={isAdmin?"Review customer orders and keep their status up to date.":"Track your purchases and check the latest order status."}/>
    {error&&<p className="mb-4 rounded-xl bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
    {busy?<div className="panel h-56 animate-pulse"/>:orders.length===0?<EmptyState title="No orders yet" description="Orders you place will appear here."/>:
    <div className="panel overflow-hidden"><div className="overflow-x-auto"><table className="w-full min-w-[700px] text-left"><thead className="bg-slate-50 text-[10px] font-extrabold uppercase tracking-wider text-slate-400"><tr><th className="px-6 py-4">Order</th>{isAdmin&&<th className="px-6 py-4">Customer</th>}<th className="px-6 py-4">Date</th><th className="px-6 py-4">Items</th><th className="px-6 py-4">Total</th><th className="px-6 py-4">Status</th><th className="px-6 py-4">Action</th></tr></thead>
      <tbody className="divide-y divide-slate-100 text-sm">{orders.map(o=><tr key={o.id} className="hover:bg-slate-50/70"><td className="px-6 py-4 font-extrabold text-slate-800">#{String(o.id).slice(0,8)}</td>{isAdmin&&<td className="px-6 py-4 text-slate-600">{o.customerName}</td>}<td className="px-6 py-4 text-slate-500">{new Date(o.createdAt).toLocaleDateString()}</td><td className="px-6 py-4 text-slate-500">{o.itemCount} items</td><td className="px-6 py-4 font-bold">${Number(o.total).toFixed(2)}</td><td className="px-6 py-4"><StatusBadge status={o.status}/></td><td className="px-6 py-4"><div className="flex items-center gap-2"><Link to={`/orders/${o.id}`} className="rounded-lg p-2 text-indigo-600 hover:bg-indigo-50"><ArrowUpRight size={17}/></Link>{isAdmin&&<select className="max-w-32 rounded-lg border border-slate-200 p-2 text-xs" value={o.status} onChange={e=>changeStatus(o.id,e.target.value)}><option>Pending</option><option>Confirmed</option><option>Shipped</option><option>Delivered</option><option>Cancelled</option></select>}</div></td></tr>)}</tbody>
    </table></div></div>}
  </div>;
}
