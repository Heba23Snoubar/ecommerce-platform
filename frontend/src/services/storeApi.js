import { USE_MOCK } from "../config";
import { http } from "./api";
import { mockApi } from "./mockApi";
import { storage } from "../utils/storage";

// All backend-specific knowledge lives here. Pages keep using the same simple
// shapes ({ name, price, category, stock, ... }) and this file translates.

const body = (r) => r.data?.data ?? r.data;
const first = (o, ...keys) => {
  for (const k of keys) {
    if (o?.[k] !== undefined && o?.[k] !== null) return o[k];
  }
  return undefined;
};
const listOf = (d) => (Array.isArray(d) ? d : d?.items ?? d?.Items ?? []);
const ORDER_STATUS = ["Pending", "Confirmed", "Shipped", "Delivered", "Cancelled"];
const statusName = (s) => (typeof s === "number" ? ORDER_STATUS[s] ?? String(s) : String(s));

// ---- categories (products only carry categoryId, so we cache id -> name) ----
let categoryCache = null;
const loadCategories = async (force = false) => {
  if (!categoryCache || force) {
    const d = body(await http.get("/categories"));
    categoryCache = listOf(d).map((c) => ({
      id: c.id ?? c.Id,
      name: c.name ?? c.Name,
      description: c.description ?? c.Description ?? "",
      imageUrl: c.imageUrl ?? c.ImageUrl ?? null,
      parentId: c.parentId ?? c.ParentId ?? null
    }));
  }
  return categoryCache;
};

const mapUser = (u) => ({
  id: u?.id ?? u?.Id,
  name: u?.fullName ?? u?.FullName ?? u?.name ?? u?.email ?? u?.Email ?? "User",
  email: u?.email ?? u?.Email,
  role: String(u?.role ?? u?.Role ?? "Customer")
});

const mapProduct = (p, cats = []) => {
  const stock = first(p, "stock", "quantity", "availableQuantity");
  const image =
    p?.mainImageUrl ??
    p?.MainImageUrl ??
    p?.images?.find((i) => i.isMain || i.IsMain)?.imageUrl ??
    p?.Images?.find((i) => i.isMain || i.IsMain)?.imageUrl ??
    p?.images?.[0]?.imageUrl ??
    p?.Images?.[0]?.imageUrl ??
    "";
  const catId = p?.categoryId ?? p?.CategoryId;
  const foundCat = cats.find((c) => (c.id ?? c.Id) === catId);
  return {
    id: p?.id ?? p?.Id,
    name: p?.name ?? p?.Name,
    description: p?.description ?? p?.Description ?? "",
    price: Number(p?.price ?? p?.Price ?? 0),
    categoryId: catId,
    category: foundCat?.name ?? foundCat?.Name ?? "Uncategorized",
    image,
    stock: typeof stock === "number" ? stock : null,
    inStock: typeof stock === "number" ? stock > 0 : (p?.isAvailable ?? p?.IsAvailable ?? true)
  };
};

const mapOrder = (o) => {
  const items = (o?.items ?? o?.Items ?? []).map((i) => ({
    productId: i?.productId ?? i?.ProductId,
    name: i?.productName ?? i?.ProductName ?? i?.name ?? i?.Name ?? "Product",
    unitPrice: Number(i?.unitPrice ?? i?.UnitPrice ?? 0),
    quantity: Number(i?.quantity ?? i?.Quantity ?? 1)
  }));
  return {
    id: o?.id ?? o?.Id,
    userId: o?.userId ?? o?.UserId,
    customerName:
      o?.customerName ??
      o?.CustomerName ??
      `Customer ${String(o?.userId ?? o?.UserId ?? "").slice(0, 8)}`,
    status: statusName(o?.status ?? o?.Status),
    total: Number(o?.total ?? o?.Total ?? items.reduce((s, i) => s + i.unitPrice * i.quantity, 0)),
    createdAt: o?.createdAt ?? o?.CreatedAt ?? new Date().toISOString(),
    itemCount: o?.itemCount ?? o?.ItemCount ?? items.reduce((s, i) => s + i.quantity, 0),
    items
  };
};

const real = {
  async login({ email, password }) {
    const d = body(await http.post("/users/login", { email, password }));
    if (d.refreshToken) storage.set("shopflow_refresh_token", d.refreshToken);
    return { token: d.accessToken, user: mapUser(d.user) };
  },

  async register({ name, email, password }) {
    const d = body(await http.post("/users/register", { fullName: name, email, password }));
    if (d.refreshToken) storage.set("shopflow_refresh_token", d.refreshToken);
    return { token: d.accessToken, user: mapUser(d.user) };
  },

  async getMe() {
    const d = body(await http.get("/users/me"));
    return mapUser(d);
  },

  async logout() {
    const refreshToken = storage.get("shopflow_refresh_token");
    if (refreshToken) {
      await http.post("/users/logout", { refreshToken }).catch(() => {});
    }
  },

  async getCategories() {
    return (await loadCategories()).map((c) => c.name);
  },

  async getCategoryList() {
    return loadCategories(true);
  },

  async addCategory({ name, description = null, imageUrl = null, parentId = null }) {
    await http.post("/categories", { name, description, imageUrl, parentId });
    return loadCategories(true);
  },

  async updateCategory(id, { name, description = null, imageUrl = null, parentId = null }) {
    await http.put(`/categories/${id}`, { name, description, imageUrl, parentId });
    return loadCategories(true);
  },

  async deleteCategory(id) {
    await http.delete(`/categories/${id}`);
    return loadCategories(true);
  },

  async getProducts({
    search = "",
    category = "All",
    page = 1,
    limit = 8,
    minPrice,
    maxPrice,
    sortBy,
    sortDescending
  } = {}) {
    const cats = await loadCategories().catch(() => []);
    const params = { pageNumber: page, pageSize: limit };
    if (search) params.searchTerm = search;
    if (category && category !== "All") {
      const c = cats.find((x) => x.name.toLowerCase() === category.toLowerCase());
      if (c) params.categoryId = c.id;
    }
    if (minPrice != null) params.minPrice = minPrice;
    if (maxPrice != null) params.maxPrice = maxPrice;
    if (sortBy) params.sortBy = sortBy;
    if (sortDescending != null) params.sortDescending = sortDescending;

    const d = body(await http.get("/products", { params }));
    const items = listOf(d).map((p) => mapProduct(p, cats));
    const total = first(d, "totalCount", "total", "totalItems", "TotalCount") ?? items.length;
    const totalPages =
      first(d, "totalPages", "TotalPages") ?? Math.max(1, Math.ceil(total / limit));
    return { items, total, page, totalPages };
  },

  async getProduct(id) {
    const cats = await loadCategories().catch(() => []);
    return mapProduct(body(await http.get(`/products/${id}`)), cats);
  },

  async addProduct(form) {
    const images = [];
    if (form.image && form.image.trim()) {
      images.push({ imageUrl: form.image.trim(), isMain: true });
    }
    const payload = {
      name: form.name.trim(),
      description: form.description?.trim() || null,
      price: Number(form.price),
      categoryId: form.categoryId,
      images
    };
    const res = body(await http.post("/products", payload));
    const createdId = res?.id ?? res?.Id ?? res;
    if (createdId && typeof createdId === "string") {
      try {
        return await real.getProduct(createdId);
      } catch {
        // fallback
      }
    }
    return mapProduct(res, await loadCategories());
  },

  async updateProduct(id, form) {
    const payload = {
      name: form.name.trim(),
      description: form.description?.trim() || null,
      price: Number(form.price),
      categoryId: form.categoryId
    };
    await http.put(`/products/${id}`, payload);
    return real.getProduct(id);
  },

  async updateProductPrice(id, price) {
    await http.patch(`/products/${id}/price`, { price: Number(price) });
    return real.getProduct(id);
  },

  async deleteProduct(id) {
    await http.delete(`/products/${id}`);
    return true;
  },

  async createOrder({ items }) {
    const payload = {
      items: items.map((i) => ({
        productId: i.productId,
        quantity: Number(i.quantity)
      }))
    };
    return mapOrder(body(await http.post("/orders", payload)));
  },

  async getOrders() {
    const d = body(await http.get("/orders", { params: { page: 1, pageSize: 50 } }));
    return listOf(d).map(mapOrder);
  },

  async getOrder(id) {
    return mapOrder(body(await http.get(`/orders/${id}`)));
  },

  async updateOrderStatus(id, status) {
    return mapOrder(body(await http.patch(`/orders/${id}/status`, { status })));
  },

  async getNotifications() {
    const d = body(await http.get("/notifications"));
    const readIds = storage.get("shopflow_read_notifications", []);
    return listOf(d).map((n) => {
      const id = n.id ?? n.Id;
      return {
        id,
        title: n.title ?? n.Title ?? "Order Update",
        message: n.message ?? n.Message,
        createdAt: n.createdAt ?? n.CreatedAt,
        read: readIds.includes(id)
      };
    });
  },

  async markNotificationRead(id) {
    const readIds = storage.get("shopflow_read_notifications", []);
    if (id && !readIds.includes(id)) {
      storage.set("shopflow_read_notifications", [...readIds, id]);
    }
    return real.getNotifications();
  },

  async pingOrders() {
    return body(await http.get("/orders/ping"));
  }
};

const mock = {
  ...mockApi,
  getMe: async () => storage.get("shopflow_user"),
  logout: async () => {},
  getCategoryList: async () => (await mockApi.getCategories()).map((n) => ({ id: n, name: n })),
  addCategory: async () => {
    return mock.getCategoryList();
  },
  updateCategory: async () => mock.getCategoryList(),
  deleteCategory: async () => mock.getCategoryList(),
  getOrder: async (id) =>
    (await mockApi.getOrders({}, true)).find((o) => o.id === id) ??
    Promise.reject(new Error("This order could not be found.")),
  updateProduct: async () => {},
  updateProductPrice: async () => {},
  deleteProduct: async () => {},
  pingOrders: async () => ({ module: "orders", status: "alive" })
};

export const storeApi = USE_MOCK ? mock : real;
