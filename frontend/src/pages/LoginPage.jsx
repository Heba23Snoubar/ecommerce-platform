import { useState } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { ArrowRight, LockKeyhole, ShoppingBag, Sparkles } from "lucide-react";
import { useAuth } from "../context/AuthContext";

export default function LoginPage() {
  const {user,login,loading}=useAuth(); const navigate=useNavigate();
  const [form,setForm]=useState({email:"",password:""});
  const [error,setError]=useState("");
  if(user) return <Navigate to="/products" replace/>;
  const change=e=>setForm({...form,[e.target.name]:e.target.value});
  const submit=async e=>{e.preventDefault();setError("");try{await login(form);navigate("/products");}catch(err){setError(err.message);}};
  return <div className="grid min-h-screen bg-white lg:grid-cols-[1.05fr_.95fr]">
    <section className="relative hidden overflow-hidden bg-[#182344] p-12 text-white lg:flex lg:flex-col lg:justify-between">
      <div className="absolute -right-28 -top-28 h-96 w-96 rounded-full border border-white/10"/><div className="absolute -bottom-40 -left-20 h-[500px] w-[500px] rounded-full border border-white/10"/>
      <div className="relative flex items-center gap-3"><div className="grid h-11 w-11 place-items-center rounded-xl bg-indigo-500"><ShoppingBag/></div><span className="font-display text-xl font-extrabold">ShopFlow</span></div>
      <div className="relative max-w-xl"><div className="mb-5 inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1.5 text-xs font-semibold text-indigo-100"><Sparkles size={14}/> A simpler way to shop</div><h1 className="font-display text-5xl font-extrabold leading-[1.15]">Everything you need.<br/><span className="text-indigo-300">One smooth flow.</span></h1><p className="mt-6 max-w-md text-base leading-7 text-slate-300">Discover thoughtful products, place orders with ease, and keep everything organized in one place.</p></div>
      <p className="relative text-xs text-slate-400">© 2026 ShopFlow. All rights reserved.</p>
    </section>
    <section className="flex items-center justify-center px-6 py-12">
      <div className="w-full max-w-[420px]">
        <div className="mb-10 lg:hidden"><div className="flex items-center gap-3 font-display text-xl font-extrabold"><span className="grid h-10 w-10 place-items-center rounded-xl bg-indigo-600 text-white"><ShoppingBag/></span>ShopFlow</div></div>
        <p className="text-xs font-extrabold uppercase tracking-[.2em] text-indigo-600">Welcome back</p><h2 className="mt-2 font-display text-3xl font-extrabold tracking-tight text-slate-900">Sign in to your account</h2><p className="mt-3 text-sm text-slate-500">Enter your details to continue to ShopFlow.</p>
        <form onSubmit={submit} className="mt-8 space-y-5">
          <div><label className="mb-2 block text-xs font-bold text-slate-700">Email address</label><input className="field" type="email" name="email" value={form.email} onChange={change} placeholder="you@example.com" required/></div>
          <div><label className="mb-2 block text-xs font-bold text-slate-700">Password</label><input className="field" type="password" name="password" value={form.password} onChange={change} placeholder="Enter your password" required/></div>
          {error&&<p className="rounded-xl bg-rose-50 px-4 py-3 text-sm text-rose-600">{error}</p>}
          <button disabled={loading} className="btn-primary flex w-full items-center justify-center gap-2 disabled:opacity-60">{loading?"Signing in...":"Sign in"}<ArrowRight size={17}/></button>
        </form>
        <p className="mt-7 text-center text-sm text-slate-500">New to ShopFlow? <Link className="font-bold text-indigo-600 hover:text-indigo-700" to="/register">Create an account</Link></p>
        <div className="mt-8 flex items-center justify-center gap-2 text-xs text-slate-400"><LockKeyhole size={13}/> Secure access to your workspace</div>
      </div>
    </section>
  </div>;
}
