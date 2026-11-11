import { createContext, useContext, useMemo, useState } from "react";
import { storage } from "../utils/storage";
const StoreContext = createContext(null);
export function StoreProvider({children}) {
  const [selection,setSelection] = useState(()=>storage.get("shopflow_selection",[]));
  const update = value => { setSelection(value); storage.set("shopflow_selection",value); };
  const addItem = (product,quantity=1) => {
    const current=[...selection];
    const found=current.find(i=>i.productId===product.id);
    if(found) found.quantity=Math.min(found.quantity+quantity,product.stock??Infinity);
    else current.push({productId:product.id,name:product.name,unitPrice:Number(product.price),quantity:Math.min(quantity,product.stock??Infinity)});
    update(current);
  };
  const setQuantity = (id,quantity) => update(selection.map(i=>i.productId===id?{...i,quantity:Number(quantity)}:i).filter(i=>i.quantity>0));
  const removeItem = id => update(selection.filter(i=>i.productId!==id));
  const clearSelection = () => update([]);
  const total = selection.reduce((sum,i)=>sum+i.unitPrice*i.quantity,0);
  const value=useMemo(()=>({selection,addItem,setQuantity,removeItem,clearSelection,total}),[selection,total]);
  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>;
}
export const useStore = () => useContext(StoreContext);
