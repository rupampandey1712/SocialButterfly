import { defineConfig, transformWithEsbuild } from "vite";
import react from "@vitejs/plugin-react";

const sourceFilePattern = /[\\/]src[\\/].*\.[jt]sx?$/;
const jsSourceFilePattern = /[\\/]src[\\/].*\.js$/;
const apiOrigin = (process.env.VITE_API_ORIGIN ?? "https://localhost:7151").replace(/\/$/, "");

export default defineConfig({
  plugins: [
    {
      name: "configure-api-origin",
      enforce: "pre",
      transform(code, id) {
        if (!sourceFilePattern.test(id) || !code.includes("https://localhost:7151")) {
          return null;
        }

        return code.replaceAll("https://localhost:7151", apiOrigin);
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
  ],
  optimizeDeps: {
    esbuildOptions: {
      loader: {
        ".js": "jsx",
      },
    },
  },
});
