import { useState } from "react";
import { Link,useNavigate } from "react-router-dom";
import { ArrowLeft, Minus, Plus, Trash2, ShoppingBag } from "lucide-react";
import { useStore } from "../context/StoreContext";
import { useAuth } from "../context/AuthContext";
import { storeApi } from "../services/storeApi";
import EmptyState from "../components/EmptyState";
import PageHeading from "../components/PageHeading";
export default function CheckoutPage(){
  const {selection,setQuantity,removeItem,clearSelection,total}=useStore(); const {user}=useAuth(); const navigate=useNavigate(); const [error,setError]=useState(""); const [busy,setBusy]=useState(false);
  const submit=async()=>{if(!selection.length)return;setBusy(true);setError("");try{const order=await storeApi.createOrder({user,items:selection});clearSelection();navigate(`/orders/${order.id}`);}catch(e){setError(e.message);}finally{setBusy(false);}};
  if(!selection.length)return <EmptyState title="No products selected" description="Choose products and quantities before placing an order." action={<Link to="/products" className="btn-primary">Explore products</Link>}/>;
  return <div><Link to="/products" className="mb-5 inline-flex items-center gap-2 text-sm font-bold text-slate-500"><ArrowLeft size={16}/>Continue shopping</Link><PageHeading eyebrow="Your selection" title="Review your order" description="Check your selected products and quantities before placing the order."/>
    <div className="grid gap-6 lg:grid-cols-[1fr_340px]"><div className="space-y-3">{selection.map(item=><div key={item.productId} className="panel flex flex-wrap items-center justify-between gap-4 p-4"><div className="min-w-[150px] flex-1"><h3 className="text-sm font-extrabold text-slate-800">{item.name}</h3><p className="mt-1 text-xs text-slate-400">${item.unitPrice.toFixed(2)} each</p></div><div className="flex items-center rounded-xl border border-slate-200"><button className="px-3 py-2" onClick={()=>setQuantity(item.productId,item.quantity-1)}><Minus size={14}/></button><span className="min-w-7 text-center text-sm font-bold">{item.quantity}</span><button className="px-3 py-2" onClick={()=>setQuantity(item.productId,item.quantity+1)}><Plus size={14}/></button></div><div className="w-24 text-right text-sm font-extrabold">${(item.unitPrice*item.quantity).toFixed(2)}</div><button onClick={()=>removeItem(item.productId)} className="rounded-lg p-2 text-slate-300 hover:bg-rose-50 hover:text-rose-500"><Trash2 size={17}/></button></div>)}</div>
      <aside className="panel h-fit p-6"><h2 className="font-display text-lg font-extrabold">Order summary</h2><div className="mt-5 flex justify-between text-sm text-slate-500"><span>Items</span><span>{selection.reduce((s,i)=>s+i.quantity,0)}</span></div><div className="mt-3 flex justify-between text-sm text-slate-500"><span>Shipping</span><span>Calculated later</span></div><div className="my-5 border-t border-slate-100"/><div className="flex justify-between"><span className="font-bold text-slate-700">Total</span><span className="font-display text-xl font-extrabold">${total.toFixed(2)}</span></div>{error&&<p className="mt-4 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-600">{error}</p>}<button onClick={submit} disabled={busy} className="btn-primary mt-6 flex w-full items-center justify-center gap-2 disabled:opacity-50"><ShoppingBag size={17}/>{busy?"Placing order...":"Place order"}</button><p className="mt-3 text-center text-[11px] leading-5 text-slate-400">Your order will be submitted for processing.</p></aside>
    </div>
  </div>;
}
