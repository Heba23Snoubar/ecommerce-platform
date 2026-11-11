export const seedProducts = [
  { id:"p1", name:"Studio Wireless Headphones", category:"Electronics", price:89.99, stock:24, image:"https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=900", description:"Immersive sound, soft ear cushions and a clean everyday design.", featured:true },
  { id:"p2", name:"Everyday Backpack", category:"Accessories", price:54.00, stock:18, image:"https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=900", description:"A lightweight backpack with practical storage for everyday essentials.", featured:true },
  { id:"p3", name:"Minimal Desk Lamp", category:"Home", price:42.50, stock:12, image:"https://images.unsplash.com/photo-1508004680771-708b02aabdc0?w=900", description:"A warm, minimal lamp for focused work and relaxed evenings." },
  { id:"p4", name:"Ceramic Coffee Set", category:"Home", price:36.00, stock:31, image:"https://images.unsplash.com/photo-1514228742587-6b1558f84478?w=900", description:"A simple ceramic set made for slow mornings and good coffee." },
  { id:"p5", name:"Classic Running Shoes", category:"Fashion", price:74.90, stock:9, image:"https://images.unsplash.com/photo-1542291026-7eec264c27ff?w=900", description:"Comfortable everyday trainers with a timeless silhouette." },
  { id:"p6", name:"Smart Watch Active", category:"Electronics", price:119.00, stock:15, image:"https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=900", description:"Track your day with a crisp display and useful daily features." },
  { id:"p7", name:"Leather Card Holder", category:"Accessories", price:28.00, stock:42, image:"https://images.unsplash.com/photo-1627123424574-724758594e93?w=900", description:"A compact card holder with a refined, understated finish." },
  { id:"p8", name:"Soft Throw Blanket", category:"Home", price:48.00, stock:7, image:"https://images.unsplash.com/photo-1600210492486-724fe5c67fb0?w=900", description:"A soft textured throw that brings comfort to any room." }
];

export const seedOrders = [
  { id:"ORD-1048", userId:"demo-customer", customerName:"Alex Morgan", items:[{productId:"p1",name:"Studio Wireless Headphones",quantity:1,unitPrice:89.99}], total:89.99, status:"Pending", createdAt:new Date(Date.now()-86400000).toISOString() },
  { id:"ORD-1047", userId:"demo-customer", customerName:"Alex Morgan", items:[{productId:"p2",name:"Everyday Backpack",quantity:2,unitPrice:54}], total:108, status:"Shipped", createdAt:new Date(Date.now()-3*86400000).toISOString() }
];
export const seedNotifications = [
  { id:"n1", title:"Welcome to ShopFlow", message:"Your account is ready. Explore the latest products.", createdAt:new Date().toISOString(), read:false },
  { id:"n2", title:"Order update", message:"Your order ORD-1047 has been shipped.", createdAt:new Date(Date.now()-86400000).toISOString(), read:false }
];
