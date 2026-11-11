import { PackageOpen } from "lucide-react";
export default function EmptyState({title="Nothing here yet",description="There is no data to display.",action}) {
  return <div className="panel flex flex-col items-center justify-center px-6 py-16 text-center">
    <div className="mb-4 rounded-2xl bg-indigo-50 p-4 text-indigo-500"><PackageOpen size={28}/></div>
    <h3 className="font-display text-lg font-extrabold text-slate-800">{title}</h3>
    <p className="mt-2 max-w-sm text-sm text-slate-500">{description}</p>
    {action && <div className="mt-5">{action}</div>}
  </div>;
}
