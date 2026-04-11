import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": {
        target: "http://localhost:5275",
        changeOrigin: true,
        secure: false,
      },
      "/hubs": {
        target: "http://localhost:5275",
        changeOrigin: true,
        secure: false,
        ws: true,
      },
      "/health": {
        target: "http://localhost:5275",
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    outDir: "../wwwroot",
    emptyOutDir: true,
  },
});
