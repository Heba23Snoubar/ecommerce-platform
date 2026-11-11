import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { Bell, LogOut, Package, ShoppingBag, ShoppingCart, Plus, Menu } from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { useState } from "react";
import { useStore } from "../context/StoreContext";

export default function AppLayout() {
  const {user,isAdmin,logout}=useAuth();
  const {selection}=useStore();
  const [open,setOpen]=useState(false);
  const navigate=useNavigate();
  const links=[
    {to:"/products",label:"Products",icon:Package},
    {to:"/orders",label:isAdmin?"All Orders":"My Orders",icon:ShoppingBag},
    ...(isAdmin?[{to:"/products/new",label:"Add Product",icon:Plus}]:[]),
    {to:"/notifications",label:"Notifications",icon:Bell}
  ];
  const linkClass=({isActive})=>`flex items-center gap-3 rounded-xl px-3 py-3 text-sm font-semibold transition ${isActive?"bg-indigo-50 text-indigo-700":"text-slate-500 hover:bg-slate-50 hover:text-slate-800"}`;
  const sidebar=<>
    <div className="flex h-[76px] items-center gap-3 border-b border-slate-100 px-6">
      <div className="grid h-10 w-10 place-items-center rounded-xl bg-indigo-600 text-white"><ShoppingBag size={21}/></div>
      <div><div className="font-display text-lg font-extrabold tracking-tight">ShopFlow</div><div className="text-[10px] font-bold uppercase tracking-[.18em] text-slate-400">Simple commerce</div></div>
    </div>
    <div className="px-4 pt-7">
      <p className="mb-3 px-3 text-[10px] font-extrabold uppercase tracking-[.18em] text-slate-400">Workspace</p>
      <nav className="space-y-1">{links.map(({to,label,icon:Icon})=><NavLink key={to} to={to} end className={linkClass} onClick={()=>setOpen(false)}><Icon size={18}/>{label}</NavLink>)}</nav>
    </div>
    <div className="mt-auto p-4">
      <div className="rounded-2xl bg-slate-50 p-4">
        <div className="mb-1 text-xs font-bold text-slate-700">Need a hand?</div>
        <p className="text-xs leading-5 text-slate-500">Your store workspace, all in one place.</p>
      </div>
      <button onClick={()=>{logout();navigate("/login");}} className="mt-4 flex w-full items-center gap-3 rounded-xl px-3 py-3 text-sm font-semibold text-slate-500 hover:bg-rose-50 hover:text-rose-600"><LogOut size={18}/>Sign out</button>
    </div>
  </>;
  return <div className="min-h-screen bg-[#f7f8fa]">
    <aside className="fixed inset-y-0 left-0 z-30 hidden w-[250px] flex-col border-r border-slate-100 bg-white lg:flex">{sidebar}</aside>
    {open&&<div className="fixed inset-0 z-40 bg-slate-900/30 lg:hidden" onClick={()=>setOpen(false)}><aside className="flex h-full w-[270px] flex-col bg-white" onClick={e=>e.stopPropagation()}>{sidebar}</aside></div>}
    <div className="min-h-screen lg:pl-[250px]">
      <header className="sticky top-0 z-20 flex h-[76px] items-center justify-between border-b border-slate-100 bg-white/90 px-5 backdrop-blur md:px-8">
        <div className="flex items-center gap-3">
          <button className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 lg:hidden" onClick={()=>setOpen(true)}><Menu size={20}/></button>
          <div><div className="font-display text-sm font-extrabold text-slate-800">Good day, {user?.name?.split(" ")[0] || "there"} 👋</div><div className="text-xs text-slate-400">Here's what's happening in your store.</div></div>
        </div>
        <div className="flex items-center gap-3">
          {!isAdmin&&<button onClick={()=>navigate("/checkout")} className="relative rounded-xl border border-slate-100 p-2.5 text-slate-600 hover:bg-slate-50"><ShoppingCart size={18}/>{selection.length>0&&<span className="absolute -right-1 -top-1 grid h-4 min-w-4 place-items-center rounded-full bg-indigo-600 px-1 text-[9px] font-bold text-white">{selection.length}</span>}</button>}
          <div className="hidden text-right sm:block"><div className="text-sm font-bold text-slate-700">{user?.name}</div><div className="text-[11px] text-slate-400">{user?.role}</div></div>
          <div className="grid h-10 w-10 place-items-center rounded-full bg-indigo-100 text-sm font-extrabold text-indigo-700">{user?.name?.split(" ").map(x=>x[0]).slice(0,2).join("")||"U"}</div>
        </div>
      </header>
      <main className="mx-auto max-w-[1440px] p-5 md:p-8"><Outlet/></main>
    </div>
  </div>;
}
