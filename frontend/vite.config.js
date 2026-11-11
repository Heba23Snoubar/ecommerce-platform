import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  // Where the ASP.NET API listens while developing (default: http://localhost:5097).
  const target = env.VITE_PROXY_TARGET || "http://localhost:5097";
  return {
    plugins: [react(), tailwindcss()],
    server: {
      proxy: {
        "/api": {
          target,
          changeOrigin: true,
          secure: false
        }
      }
    }
  };
});
