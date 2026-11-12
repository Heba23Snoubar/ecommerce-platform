import { useEffect, useState } from "react";
import { Search, SlidersHorizontal, Plus } from "lucide-react";
import { Link } from "react-router-dom";
import { storeApi } from "../services/storeApi";
import { useAuth } from "../context/AuthContext";
import ProductCard from "../components/ProductCard";
import PageHeading from "../components/PageHeading";
import EmptyState from "../components/EmptyState";
import { PAGE_SIZE } from "../config";

export default function ProductsPage(){
  const {isAdmin}=useAuth(); const [result,setResult]=useState({items:[],total:0,totalPages:1}); const [search,setSearch]=useState(""); const [category,setCategory]=useState("All"); const [page,setPage]=useState(1); const [error,setError]=useState(""); const [busy,setBusy]=useState(true); const [categories,setCategories]=useState([]);
  useEffect(()=>{storeApi.getCategories().then(data=>setCategories(data)).catch(()=>{});},[]);
  useEffect(()=>{let active=true;storeApi.getProducts({search,category,page,limit:PAGE_SIZE}).then(data=>{if(active)setResult(data);}).catch(e=>{if(active)setError(e.message);}).finally(()=>active&&setBusy(false));return()=>{active=false};},[search,category,page]);
  const handleSearch=val=>{setSearch(val);setPage(1);setBusy(true);};
  const handleCategory=c=>{setCategory(c);setPage(1);setBusy(true);};
  const handlePage=updater=>{setPage(p=>{const next=typeof updater==="function"?updater(p):updater;setBusy(true);return next;});};
  return <div>
    <PageHeading eyebrow="Discover" title="Explore products" description="Find something you'll love. Browse our collection and check live stock availability." action={isAdmin&&<Link to="/products/new" className="btn-primary inline-flex items-center gap-2"><Plus size={17}/>Add product</Link>}/>
    <div className="panel mb-6 flex flex-col gap-3 p-3 md:flex-row md:items-center md:justify-between">
      <div className="relative w-full md:max-w-sm"><Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={17}/><input className="field pl-10" value={search} onChange={e=>handleSearch(e.target.value)} placeholder="Search products..."/></div>
      <div className="flex items-center gap-2 overflow-x-auto scrollbar-hide"><SlidersHorizontal size={16} className="shrink-0 text-slate-400"/>{["All",...categories].map(c=><button key={c} onClick={()=>handleCategory(c)} className={`whitespace-nowrap rounded-lg px-3 py-2 text-xs font-bold transition ${category===c?"bg-indigo-600 text-white":"bg-slate-50 text-slate-500 hover:bg-slate-100"}`}>{c}</button>)}</div>
    </div>
    {error&&<p className="mb-4 rounded-xl bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
    <div className="mb-4 flex items-center justify-between"><p className="text-xs font-semibold text-slate-400">{result.total} products found</p><p className="text-xs text-slate-400">Showing page {page} of {result.totalPages}</p></div>
    {busy?<div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">{Array.from({length:8}).map((_,i)=><div key={i} className="h-[280px] animate-pulse rounded-2xl bg-slate-200/70"/>)}</div>:result.items.length?<div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">{result.items.map(p=><ProductCard key={p.id} product={p}/>)}</div>:<EmptyState title="No products found" description="Try another search term or choose a different category."/>}
    <div className="mt-8 flex items-center justify-center gap-3"><button className="btn-secondary text-sm disabled:opacity-40" disabled={page<=1} onClick={()=>handlePage(p=>p-1)}>Previous</button><span className="text-xs font-bold text-slate-500">{page} / {result.totalPages}</span><button className="btn-secondary text-sm disabled:opacity-40" disabled={page>=result.totalPages} onClick={()=>handlePage(p=>p+1)}>Next</button></div>
  </div>;
}
