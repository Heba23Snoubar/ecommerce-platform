import { ArrowUpRight, Package } from "lucide-react";
import { Link } from "react-router-dom";
export default function ProductCard({product}) {
  const inStock=product.inStock??product.stock>0;
  const label=!inStock?"Out of stock":(product.stock!=null?`${product.stock} available`:"In stock");
  return <Link to={`/products/${product.id}`} className="group overflow-hidden rounded-2xl border border-slate-100 bg-white transition duration-200 hover:-translate-y-1 hover:shadow-xl hover:shadow-slate-200/60">
    <div className="product-image relative aspect-[1.25] overflow-hidden">
      <img src={product.image||"https://placehold.co/600x450?text=Product"} alt={product.name} className="h-full w-full object-cover transition duration-500 group-hover:scale-105"/>
      <span className="absolute left-3 top-3 rounded-full bg-white/90 px-3 py-1 text-[10px] font-extrabold uppercase tracking-wider text-slate-600 backdrop-blur">{product.category}</span>
    </div>
    <div className="p-4">
      <div className="flex items-start justify-between gap-3"><h3 className="line-clamp-2 text-sm font-extrabold leading-5 text-slate-800">{product.name}</h3><ArrowUpRight size={17} className="shrink-0 text-slate-300 transition group-hover:text-indigo-600"/></div>
      <div className="mt-3 flex items-center justify-between"><span className="font-display text-lg font-extrabold text-slate-900">${Number(product.price).toFixed(2)}</span><span className={`flex items-center gap-1 text-[11px] font-semibold ${inStock?"text-emerald-600":"text-rose-500"}`}><Package size={13}/>{label}</span></div>
    </div>
  </Link>;
}
