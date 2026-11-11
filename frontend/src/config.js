// Real API by default. Set VITE_USE_MOCK=true in .env to work offline with demo data.
export const USE_MOCK = import.meta.env.VITE_USE_MOCK === "true";
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api/v1";
export const APP_NAME = "ShopFlow";
export const PAGE_SIZE = 8;
