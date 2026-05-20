import path from "node:path";
import fs from "node:fs";
import { defineConfig, type Plugin } from "vite";

// Resolves to <repo>/vendor/replays. The viewer lives at
// `<repo>/tools/replay-viewer`, so two parents up.
const REPLAYS_ROOT = path.resolve(__dirname, "../../vendor/replays");

// Tiny dev-server middleware that exposes `vendor/replays/` at `/replays/`
// (read-only). The viewer fetches `/replays/runs.json` on load and then
// pulls per-run artifacts under `/replays/<rel-path>/`. A static build
// (`vite build`) doesn't include this mount — that mode falls back to
// the file-picker UI.
function replaysFileMount(): Plugin {
  return {
    name: "sts2-replays-mount",
    configureServer(server) {
      server.middlewares.use("/replays", (req, res, next) => {
        if (req.method !== "GET" && req.method !== "HEAD") {
          next();
          return;
        }
        // req.url is the path AFTER the mount, e.g. "/runs.json" or
        // "/v0.103.2/1715200000-deadbeef-12345/manifest.json".
        const rel = decodeURIComponent((req.url ?? "/").split("?", 1)[0]!).replace(/^\/+/, "");
        // Hard-stop on path traversal — a normalized path that escapes
        // REPLAYS_ROOT is the only way a `..` segment can be malicious
        // here, and we serve only that subtree.
        const target = path.normalize(path.join(REPLAYS_ROOT, rel));
        if (!target.startsWith(REPLAYS_ROOT)) {
          res.statusCode = 403;
          res.end("forbidden");
          return;
        }
        fs.stat(target, (statErr, stat) => {
          if (statErr || !stat.isFile()) {
            res.statusCode = 404;
            res.end("not found");
            return;
          }
          // Set permissive headers — these are local-dev files; CORS
          // doesn't matter and the viewer is on the same origin.
          const ext = path.extname(target).toLowerCase();
          if (ext === ".json") res.setHeader("Content-Type", "application/json");
          else if (ext === ".mcr") res.setHeader("Content-Type", "application/octet-stream");
          res.setHeader("Cache-Control", "no-store");
          if (req.method === "HEAD") {
            res.end();
            return;
          }
          fs.createReadStream(target).pipe(res);
        });
      });
    },
  };
}

// Static build. The viewer is asset-free at v1 and loads replay data
// either via the in-page file picker (still works in any deployment) or
// — when run under `vite dev` — by fetching `/replays/...` paths the
// dev plugin above serves out of `<repo>/vendor/replays/`.
export default defineConfig({
  root: ".",
  plugins: [replaysFileMount()],
  server: {
    fs: {
      // Allow the dev server to read files outside `tools/replay-viewer/`.
      // Needed because vendor/replays sits at the repo root.
      allow: [path.resolve(__dirname, "../.."), __dirname],
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    target: "es2022",
  },
});
