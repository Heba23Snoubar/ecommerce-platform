import { useEffect,useState } from "react";
import { Link,useParams } from "react-router-dom";
import { ArrowLeft, Package, ShieldCheck, ShoppingBag } from "lucide-react";
import { storeApi } from "../services/storeApi";
import { useStore } from "../context/StoreContext";
import { useAuth } from "../context/AuthContext";
export default function ProductDetailsPage(){
  const {id}=useParams(); const [product,setProduct]=useState(null); const [qty,setQty]=useState(1); const [error,setError]=useState(""); const [message,setMessage]=useState("");
  const {addItem}=useStore(); const {isAdmin}=useAuth();
  useEffect(()=>{storeApi.getProduct(id).then(setProduct).catch(e=>setError(e.message));},[id]);
  if(error)return <div className="rounded-xl bg-rose-50 p-4 text-rose-600">{error}</div>;
  if(!product)return <div className="animate-pulse rounded-2xl bg-slate-200 p-32"/>;
  const inStock=product.inStock??product.stock>0;
  const stockLabel=!inStock?"Currently out of stock":(product.stock!=null?`${product.stock} units available`:"In stock");
  const add=()=>{addItem(product,qty);setMessage("Added to your order selection.");};
  return <div><Link to="/products" className="mb-6 inline-flex items-center gap-2 text-sm font-bold text-slate-500 hover:text-indigo-600"><ArrowLeft size={16}/>Back to products</Link>
    <div className="grid gap-8 lg:grid-cols-2">
      <div className="product-image overflow-hidden rounded-3xl"><img src={product.image||"https://placehold.co/900x700?text=Product"} alt={product.name} className="h-full max-h-[560px] w-full object-cover"/></div>
      <div className="flex flex-col justify-center py-3"><span className="mb-4 w-fit rounded-full bg-indigo-50 px-3 py-1.5 text-xs font-extrabold uppercase tracking-wider text-indigo-600">{product.category}</span><h1 className="font-display text-3xl font-extrabold leading-tight text-slate-900 md:text-4xl">{product.name}</h1><p className="mt-5 text-3xl font-extrabold text-slate-900">${Number(product.price).toFixed(2)}</p><p className="mt-5 max-w-lg text-sm leading-7 text-slate-500">{product.description}</p>
        <div className="mt-7 flex items-center gap-2 text-sm font-semibold text-emerald-700"><Package size={17}/>{stockLabel}</div>
        <div className="my-7 h-px bg-slate-100"/>
        {!isAdmin&&<><div className="flex items-center gap-4"><label className="text-sm font-bold text-slate-700">Quantity</label><div className="flex items-center rounded-xl border border-slate-200"><button className="px-4 py-2 text-lg" onClick={()=>setQty(q=>Math.max(1,q-1))}>−</button><span className="min-w-8 text-center text-sm font-bold">{qty}</span><button className="px-4 py-2 text-lg" onClick={()=>setQty(q=>Math.min(product.stock??99,q+1))}>+</button></div></div>
        <button disabled={!inStock} onClick={add} className="btn-primary mt-6 flex w-full max-w-sm items-center justify-center gap-2 disabled:opacity-40"><ShoppingBag size={17}/>Add to order</button>{message&&<p className="mt-3 text-sm font-semibold text-emerald-600">{message}</p>}</>}
        <div className="mt-8 flex items-center gap-2 text-xs text-slate-400"><ShieldCheck size={16}/>Stock availability is provided by the warehouse service.</div>
      </div>
    </div>
  </div>;
}
