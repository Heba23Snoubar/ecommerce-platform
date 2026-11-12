import { useState } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
export default function RegisterPage(){
  const {user,register,loading}=useAuth(); const navigate=useNavigate();
  const [form,setForm]=useState({name:"",email:"",password:""}); const [error,setError]=useState("");
  if(user)return <Navigate to="/products" replace/>;
  const change=e=>setForm({...form,[e.target.name]:e.target.value});
  const submit=async e=>{e.preventDefault();setError("");try{await register(form);navigate("/products");}catch(err){setError(err.message);}};
  return <div className="flex min-h-screen items-center justify-center bg-[#f7f8fa] px-5 py-12"><div className="panel w-full max-w-[460px] p-8 md:p-10">
    <div className="mb-8"><div className="mb-6 text-sm font-extrabold text-indigo-600">SHOPFLOW</div><h1 className="font-display text-3xl font-extrabold text-slate-900">Create your account</h1><p className="mt-2 text-sm text-slate-500">Join us and discover a better way to shop.</p></div>
    <form onSubmit={submit} className="space-y-4">
      <div><label className="mb-2 block text-xs font-bold">Full name</label><input className="field" name="name" minLength="3" value={form.name} onChange={change} required placeholder="Your name"/></div>
      <div><label className="mb-2 block text-xs font-bold">Email address</label><input className="field" type="email" name="email" value={form.email} onChange={change} required placeholder="you@example.com"/></div>
      <div><label className="mb-2 block text-xs font-bold">Password</label><input className="field" type="password" minLength="8" name="password" value={form.password} onChange={change} required placeholder="At least 8 characters"/></div>
      {error&&<p className="rounded-xl bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
      <button className="btn-primary w-full" disabled={loading}>{loading?"Creating account...":"Create account"}</button>
    </form>
    <p className="mt-6 text-center text-sm text-slate-500">Already have an account? <Link to="/login" className="font-bold text-indigo-600">Sign in</Link></p>
  </div></div>;
}
