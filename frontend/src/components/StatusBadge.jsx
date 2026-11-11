const styles={Pending:"bg-amber-50 text-amber-700",Confirmed:"bg-blue-50 text-blue-700",Shipped:"bg-violet-50 text-violet-700",Delivered:"bg-emerald-50 text-emerald-700",Cancelled:"bg-rose-50 text-rose-700"};
export default function StatusBadge({status}) {
  return <span className={`inline-flex rounded-full px-3 py-1 text-xs font-bold ${styles[status]||"bg-slate-100 text-slate-600"}`}>{status}</span>;
}
