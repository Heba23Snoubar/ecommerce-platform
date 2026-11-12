import { useEffect, useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { ArrowLeft, ImagePlus } from "lucide-react";
import { storeApi } from "../services/storeApi";
import PageHeading from "../components/PageHeading";

export default function AddProductPage(){
  const navigate=useNavigate();
  const [categories,setCategories]=useState([]);
  const [newCategory,setNewCategory]=useState("");
  const [form,setForm]=useState({name:"",categoryId:"",price:"",stock:"",image:"",description:""});
  const [error,setError]=useState(""); const [busy,setBusy]=useState(false);
  const applyCategories=list=>{setCategories(list);setForm(f=>({...f,categoryId:list.some(c=>c.id===f.categoryId)?f.categoryId:(list[0]?.id||"")}));};
  useEffect(()=>{storeApi.getCategoryList().then(applyCategories).catch(e=>setError(e.message));},[]);
  const change=e=>setForm({...form,[e.target.name]:e.target.value});
  const addCategory=async()=>{if(!newCategory.trim())return;setError("");try{applyCategories(await storeApi.addCategory({name:newCategory.trim()}));setNewCategory("");}catch(e){setError(e.message);}};
  const submit=async e=>{e.preventDefault();setBusy(true);setError("");try{await storeApi.addProduct(form);navigate("/products");}catch(err){setError(err.message);}finally{setBusy(false);}};
  return <div><Link to="/products" className="mb-5 inline-flex items-center gap-2 text-sm font-bold text-slate-500"><ArrowLeft size={16}/>Back to products</Link><PageHeading eyebrow="Inventory" title="Add a new product" description="Add product information and make it available in your store."/>
    <form onSubmit={submit} className="panel max-w-3xl p-6 md:p-8"><div className="grid gap-5 md:grid-cols-2">
      <div className="md:col-span-2"><label className="mb-2 block text-xs font-bold">Product name</label><input className="field" name="name" value={form.name} onChange={change} required placeholder="e.g. Wireless Headphones"/></div>
      <div><label className="mb-2 block text-xs font-bold">Category</label>
        <select className="field" name="categoryId" value={form.categoryId} onChange={change} required>{categories.length===0&&<option value="">No categories yet</option>}{categories.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select>
        <div className="mt-2 flex gap-2"><input className="field" value={newCategory} onChange={e=>setNewCategory(e.target.value)} placeholder="New category name"/><button type="button" className="btn-secondary whitespace-nowrap" onClick={addCategory}>Add category</button></div>
      </div>
      <div><label className="mb-2 block text-xs font-bold">Price ($)</label><input className="field" type="number" min="0.01" step="0.01" name="price" value={form.price} onChange={change} required placeholder="0.00"/></div>
      <div><label className="mb-2 block text-xs font-bold">Initial quantity</label><input className="field" type="number" min="0" name="stock" value={form.stock} onChange={change} required placeholder="0"/></div>
      <div><label className="mb-2 block text-xs font-bold">Image URL</label><input className="field" type="url" name="image" value={form.image} onChange={change} placeholder="https://..."/>{form.image&&<img src={form.image} alt="Product preview" className="mt-3 h-24 w-24 rounded-xl object-cover"/>}</div>
      <div className="md:col-span-2"><label className="mb-2 block text-xs font-bold">Description</label><textarea className="field min-h-28 resize-y" name="description" value={form.description} onChange={change} placeholder="Describe the product..."/></div>
    </div>
    {error&&<p className="mt-5 rounded-xl bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
    <div className="mt-7 flex flex-wrap justify-end gap-3"><button type="button" className="btn-secondary" onClick={()=>navigate("/products")}>Cancel</button><button disabled={busy||!form.categoryId} className="btn-primary inline-flex items-center gap-2 disabled:opacity-50"><ImagePlus size={16}/>{busy?"Saving...":"Save product"}</button></div>
    </form>
  </div>;
}
