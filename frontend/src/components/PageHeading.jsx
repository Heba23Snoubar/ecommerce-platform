export default function PageHeading({eyebrow,title,description,action}) {
  return <div className="mb-7 flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
    <div>{eyebrow&&<p className="mb-2 text-[10px] font-extrabold uppercase tracking-[.2em] text-indigo-500">{eyebrow}</p>}<h1 className="font-display text-2xl font-extrabold tracking-tight text-slate-900 md:text-[30px]">{title}</h1>{description&&<p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">{description}</p>}</div>
    {action&&<div>{action}</div>}
  </div>;
}
