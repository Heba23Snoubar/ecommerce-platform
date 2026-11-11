import { storage } from "../utils/storage";
import { seedProducts, seedOrders, seedNotifications } from "../data/seed";

const pause = (ms=180) => new Promise(resolve => setTimeout(resolve, ms));
const read = (key, fallback) => {
  const value = storage.get(key);
  if (value === null) { storage.set(key, fallback); return fallback; }
  return value;
};
const products = () => read("shopflow_products", seedProducts);
const orders = () => read("shopflow_orders", seedOrders);
const notifications = () => read("shopflow_notifications", seedNotifications);

export const mockApi = {
  async login({ email, password, role }) {
    await pause();
    if (!email || !password) throw new Error("Please enter your email and password.");
    const user = { id: role === "Admin" ? "demo-admin" : "demo-customer", name: role === "Admin" ? "Jordan Lee" : "Alex Morgan", email, role };
    return { token:"demo-jwt-token", user };
  },
  async register({ name, email, password }) {
    await pause();
    if (!name || !email || !password) throw new Error("Please complete all fields.");
    const user = { id:`user-${Date.now()}`, name, email, role:"Customer" };
    return { token:"demo-jwt-token", user };
  },
  async getProducts({ search="", category="All", page=1, limit=8 }={}) {
    await pause();
    let list = products();
    if (category !== "All") list = list.filter(p => p.category === category);
    if (search) list = list.filter(p => p.name.toLowerCase().includes(search.toLowerCase()));
    const total = list.length;
    return { items:list.slice((page-1)*limit,page*limit), total, page, totalPages:Math.max(1,Math.ceil(total/limit)) };
  },
  async getProduct(id) { await pause(); return products().find(p => p.id === id) || null; },
  async getCategories() { await pause(); return [...new Set(products().map(p => p.category))]; },
  async addProduct(data) {
    await pause();
    const list = products();
    const product = { ...data, id:`p-${Date.now()}`, stock:Number(data.stock), price:Number(data.price) };
    storage.set("shopflow_products",[product,...list]);
    return product;
  },
  async createOrder({ user, items }) {
    await pause();
    const list = products();
    for (const item of items) {
      const product = list.find(p => p.id === item.productId);
      if (!product) throw new Error(`${item.name} is no longer available.`);
      if (product.stock < item.quantity) throw new Error(`Insufficient stock for ${product.name}. Available: ${product.stock}.`);
    }
    const updated = list.map(p => {
      const item = items.find(i => i.productId === p.id);
      return item ? { ...p, stock:p.stock-item.quantity } : p;
    });
    storage.set("shopflow_products",updated);
    const total = items.reduce((sum,item)=>sum+item.quantity*item.unitPrice,0);
    const order = { id:`ORD-${Math.floor(1000+Math.random()*9000)}`, userId:user.id, customerName:user.name, items, total, status:"Pending", createdAt:new Date().toISOString() };
    storage.set("shopflow_orders",[order,...orders()]);
    storage.set("shopflow_notifications",[{id:`n-${Date.now()}`,title:"Order placed",message:`Your order ${order.id} has been created successfully.`,createdAt:new Date().toISOString(),read:false},...notifications()]);
    return order;
  },
  async getOrders(user, isAdmin=false) {
    await pause();
    const list = orders();
    return isAdmin ? list : list.filter(o => o.userId === user.id);
  },
  async updateOrderStatus(id,status) {
    await pause();
    const updated = orders().map(o => o.id === id ? {...o,status} : o);
    storage.set("shopflow_orders",updated);
    return updated.find(o=>o.id===id);
  },
  async getNotifications() { await pause(); return notifications(); },
  async markNotificationRead(id) {
    const updated = notifications().map(n=>n.id===id?{...n,read:true}:n);
    storage.set("shopflow_notifications",updated);
    return updated;
  }
};
