import { useEffect,useState } from "react";
import { Link,useParams } from "react-router-dom";
import { ArrowLeft, PackageCheck } from "lucide-react";
import { storeApi } from "../services/storeApi";
import { useAuth } from "../context/AuthContext";
import StatusBadge from "../components/StatusBadge";
export default function OrderDetailsPage(){
  const {id}=useParams(); const {isAdmin}=useAuth(); const [order,setOrder]=useState(null); const [error,setError]=useState("");
  useEffect(()=>{storeApi.getOrder(id).then(setOrder).catch(e=>setError(e.status===404?"This order could not be found.":e.message));},[id]);
  if(error)return <div className="rounded-xl bg-rose-50 p-4 text-sm text-rose-600">{error}</div>;
  if(!order)return <div className="panel h-64 animate-pulse"/>;
  return <div><Link to="/orders" className="mb-6 inline-flex items-center gap-2 text-sm font-bold text-slate-500"><ArrowLeft size={16}/>Back to orders</Link>
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4"><div><p className="text-xs font-extrabold uppercase tracking-widest text-indigo-500">Order details</p><h1 className="mt-2 font-display text-3xl font-extrabold text-slate-900">#{String(order.id).slice(0,8)}</h1><p className="mt-2 text-sm text-slate-500">Placed on {new Date(order.createdAt).toLocaleString()}</p></div><StatusBadge status={order.status}/></div>
    <div className="grid gap-6 lg:grid-cols-[1fr_320px]"><div className="panel p-6"><h2 className="mb-5 font-display text-lg font-extrabold">Items in this order</h2><div className="divide-y divide-slate-100">{order.items.map((i,index)=><div key={index} className="flex items-center justify-between gap-4 py-4 first:pt-0"><div><div className="text-sm font-bold text-slate-800">{i.name}</div><div className="mt-1 text-xs text-slate-400">Qty: {i.quantity} × ${Number(i.unitPrice).toFixed(2)}</div></div><div className="text-sm font-extrabold">${(i.quantity*i.unitPrice).toFixed(2)}</div></div>)}</div></div>
      <div className="panel h-fit p-6"><div className="mb-4 flex items-center gap-2 text-indigo-600"><PackageCheck size={19}/><h2 className="font-display font-extrabold text-slate-800">Summary</h2></div>{isAdmin&&<div className="mb-4 text-sm text-slate-500">Customer: <span className="font-bold text-slate-700">{order.customerName}</span></div>}<div className="flex justify-between border-t border-slate-100 pt-4"><span className="text-sm font-bold text-slate-600">Order total</span><span className="font-display text-xl font-extrabold">${Number(order.total).toFixed(2)}</span></div></div>
    </div>
  </div>;
}
