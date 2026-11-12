import { useEffect,useState } from "react";
import { Bell, CheckCheck } from "lucide-react";
import { storeApi } from "../services/storeApi";
import PageHeading from "../components/PageHeading";
import EmptyState from "../components/EmptyState";
export default function NotificationsPage(){
  const [items,setItems]=useState([]); const [error,setError]=useState("");
  const load=()=>storeApi.getNotifications().then(setItems).catch(e=>setError(e.message));
  useEffect(()=>{load();},[]);
  const mark=async id=>{try{const result=await storeApi.markNotificationRead(id);setItems(result);}catch(e){setError(e.message);}};
  return <div><PageHeading eyebrow="Updates" title="Notifications" description="Stay up to date with your orders and account activity." action={items.some(n=>!n.read)&&<button onClick={async()=>{for(const n of items.filter(x=>!x.read))await storeApi.markNotificationRead(n.id);load();}} className="btn-secondary inline-flex items-center gap-2"><CheckCheck size={16}/>Mark all as read</button>}/>
    {error&&<p className="mb-4 text-sm text-rose-600">{error}</p>}
    {!items.length?<EmptyState title="You're all caught up" description="New updates will appear here."/>:<div className="space-y-3">{items.map(n=><div key={n.id} className={`panel flex items-start gap-4 p-5 ${!n.read?"border-indigo-100 bg-indigo-50/30":""}`}><div className={`rounded-xl p-3 ${n.read?"bg-slate-100 text-slate-500":"bg-indigo-100 text-indigo-600"}`}><Bell size={19}/></div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center justify-between gap-2"><h3 className="text-sm font-extrabold text-slate-800">{n.title}</h3><span className="text-[11px] text-slate-400">{new Date(n.createdAt).toLocaleString()}</span></div><p className="mt-1 text-sm leading-6 text-slate-500">{n.message}</p></div>{!n.read&&<button onClick={()=>mark(n.id)} className="shrink-0 text-xs font-bold text-indigo-600 hover:text-indigo-800">Mark read</button>}</div>)}</div>}
  </div>;
}
