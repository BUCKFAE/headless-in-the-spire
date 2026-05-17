import { defineConfig } from "vite";

// Static build. The viewer is asset-free at v1 and loads replay data via
// the file-picker in the page; no API server, no asset pipeline.
//
// The dev server is also handy for pointing at a local recording
// directory: `vite --host` then drag-drop manifest.json + timeline.json
// into the page. A future iteration may add a static-serve mode that
// resolves a fixed recordings root.
export default defineConfig({
  root: ".",
  build: {
    outDir: "dist",
    emptyOutDir: true,
    target: "es2022",
  },
});
