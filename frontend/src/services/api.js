import axios from "axios";
import { API_BASE_URL } from "../config";
import { storage } from "../utils/storage";

// Error carrying the unified backend format: { code, message, details }.
export class ApiError extends Error {
  constructor(message, { status, code, details } = {}) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

const FALLBACK = {
  400: "The request was invalid. Please check your input.",
  401: "Your session is not valid. Please sign in again.",
  403: "You do not have permission to perform this action.",
  404: "The requested item could not be found.",
  409: "The request conflicts with current server state (for example, not enough stock or email already exists).",
  422: "Some of the submitted data is not valid.",
  429: "Too many attempts. Please wait a minute and try again.",
  500: "An internal server error occurred. Please try again later.",
  503: "The warehouse service is unavailable. Please try again later.",
  504: "The warehouse service took too long to respond. Please try again."
};

export const http = axios.create({
  baseURL: API_BASE_URL,
  headers: { "Content-Type": "application/json" },
  timeout: 15000
});

// Attach JWT access token to every outgoing request if available
http.interceptors.request.use((config) => {
  const token = storage.get("shopflow_token");
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Centralized response & error handling
http.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status;
    const payload = error.response?.data;

    let message =
      payload?.message ||
      payload?.error ||
      payload?.title ||
      FALLBACK[status] ||
      error.message ||
      "Something went wrong.";

    if (!error.response) {
      message = "Cannot reach the server. Please verify the backend is running and check your connection.";
    }

    // Extract validation / detail messages
    let details = undefined;
    if (Array.isArray(payload?.details) && payload.details.length > 0) {
      details = payload.details;
      message = `${message}: ${details.join(" | ")}`;
    } else if (payload?.details && typeof payload.details === "object") {
      details = Object.entries(payload.details).map(
        ([k, v]) => `${k}: ${Array.isArray(v) ? v.join(", ") : v}`
      );
      if (details.length > 0) message = `${message}: ${details.join(" | ")}`;
    } else if (payload?.errors && typeof payload.errors === "object") {
      details = Object.entries(payload.errors).flatMap(([k, v]) =>
        Array.isArray(v) ? v.map((e) => `${k}: ${e}`) : [`${k}: ${v}`]
      );
      if (details.length > 0) message = `${message}: ${details.join(" | ")}`;
    }

    // Auto-logout only on authenticated routes failing with 401 (not on login/register)
    const requestUrl = error.config?.url || "";
    const isAuthRoute =
      requestUrl.includes("/users/login") || requestUrl.includes("/users/register");

    if (status === 401 && !isAuthRoute && storage.get("shopflow_token")) {
      storage.remove("shopflow_token");
      storage.remove("shopflow_refresh_token");
      storage.remove("shopflow_user");
      window.location.assign("/login");
    }

    return Promise.reject(
      new ApiError(message, {
        status,
        code: payload?.code,
        details
      })
    );
  }
);
