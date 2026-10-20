import { defineConfig, transformWithEsbuild, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const sourceFilePattern = /[\\/]src[\\/].*\.[jt]sx?$/;
const jsSourceFilePattern = /[\\/]src[\\/].*\.js$/;
const apiOrigin = (
  process.env.VITE_API_ORIGIN ||
  loadEnv("development", process.cwd(), "").VITE_API_ORIGIN ||
  ""
).replace(/\/$/, "");

// Same hardening headers the API sends (CSP is applied by the API/nginx in production;
// Vite's dev server needs inline scripts for hot reload).
const securityHeaders = {
  "X-Content-Type-Options": "nosniff",
  "X-Frame-Options": "DENY",
  "Referrer-Policy": "strict-origin-when-cross-origin",
  "Permissions-Policy": "camera=(), microphone=(), geolocation=(), payment=()",
};

export default defineConfig({
  preview: {
    headers: securityHeaders,
  },
  server: {
    headers: securityHeaders,
    proxy: {
      "/api": {
        target: apiOrigin || "http://localhost:8080",
        changeOrigin: true,
        secure: false,
      },
      "/Images": {
        target: apiOrigin || "http://localhost:8080",
        changeOrigin: true,
        secure: false,
      },
      "/posts": {
        target: apiOrigin || "http://localhost:8080",
        changeOrigin: true,
        secure: false,
      },
      // Local media folders served by the FastAPI backend (the .NET API serves these from blob storage).
      "/stories": {
        target: apiOrigin || "http://localhost:8080",
        changeOrigin: true,
        secure: false,
      },
      "/music": {
        target: apiOrigin || "http://localhost:8080",
        changeOrigin: true,
        secure: false,
      },
      "/ai-images": {
        target: apiOrigin || "http://localhost:8080",
        changeOrigin: true,
        secure: false,
      },
    },
  },
  plugins: [
    {
      name: "configure-api-origin",
      enforce: "pre",
      transform(code, id) {
        if (!sourceFilePattern.test(id) || !code.includes("https://localhost:7151")) {
          return null;
        }

        // Legacy absolute URLs become same-origin paths: the session cookie is SameSite=Strict
        // and HttpOnly, so the browser must reach the API through this proxy (or nginx/app in
        // Docker), never cross-origin. VITE_API_ORIGIN only sets where the proxy forwards.
        return code.replaceAll("https://localhost:7151", "");
      },
    },
    {
      name: "load-js-as-jsx",
      async transform(code, id) {
        if (!jsSourceFilePattern.test(id)) {
          return null;
        }

        return transformWithEsbuild(code, id, {
          loader: "jsx",
          jsx: "automatic",
        });
      },
    },
    react(),
    tailwindcss(),
  ],
  optimizeDeps: {
    esbuildOptions: {
      loader: {
        ".js": "jsx",
      },
    },
  },
});
