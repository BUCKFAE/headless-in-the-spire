import path from "node:path";
import fs from "node:fs";
import type { ServerResponse } from "node:http";
import { defineConfig, type Plugin } from "vite";

// Resolves to <repo>/replays. The viewer lives at
// `<repo>/tools/replay-viewer`, so two parents up.
const REPLAYS_ROOT = path.resolve(__dirname, "../../replays");

// Tiny dev-server middleware that exposes `<repo>/replays/` at `/replays/`
// (read-only). The viewer fetches `/replays/runs.json` on load and then
// pulls per-run artifacts under `/replays/<rel-path>/`. A static build
// (`vite build`) doesn't include this mount — that mode falls back to
// the file-picker UI.
//
// The single `<repo>/replays/` tree holds every replay bucket: ad-hoc
// runs under `manual/`, the `record-sample-replay` demo under `sample/`,
// and the eval harness's matrix output under `eval-harness/<eval-id>/`.
// The recorder writes a `runs.json` per replay root — i.e. one per
// bucket, plus one per eval cell — but the viewer wants a single
// aggregated index. `/replays/runs.json` is therefore intercepted by
// the middleware: it walks the tree, merges every per-bucket /
// per-cell `runs.json`, and prefixes each entry's `rel_path` with the
// bucket / cell path so subsequent `/replays/<rel_path>/manifest.json`
// fetches resolve correctly. Per-bucket `runs.json` files are still
// served verbatim when fetched directly (e.g. `/replays/manual/runs.json`).
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
        // "/manual/v0.103.2/1715200000-deadbeef-12345/manifest.json".
        const rel = decodeURIComponent((req.url ?? "/").split("?", 1)[0]!).replace(/^\/+/, "");

        // Special-case the top-level aggregate. The recorder writes
        // per-bucket runs.json (replays/manual/runs.json, etc.) plus
        // one per eval cell; this synthesises the merged view the
        // viewer expects.
        if (rel === "runs.json") {
          serveAggregatedRunsIndex(res, req.method);
          return;
        }

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

interface RawRunEntry {
  rel_path?: unknown;
  started_at_unix?: unknown;
  [key: string]: unknown;
}

interface RawRunsIndex {
  version?: unknown;
  runs?: unknown;
}

function serveAggregatedRunsIndex(res: ServerResponse, method: string): void {
  res.setHeader("Content-Type", "application/json");
  res.setHeader("Cache-Control", "no-store");

  if (!fs.existsSync(REPLAYS_ROOT)) {
    // No replays/ directory at all — return an empty (but valid)
    // index. The viewer treats this as "no runs to list," not as a
    // 404 — distinct from "the dev server didn't intercept us."
    res.end(JSON.stringify({ version: 1, runs: [] }));
    return;
  }

  let version = 1;
  const aggregated: RawRunEntry[] = [];
  for (const runsJsonPath of walkForRunsJsons(REPLAYS_ROOT)) {
    const bucketRel = path
      .relative(REPLAYS_ROOT, path.dirname(runsJsonPath))
      .split(path.sep)
      .filter(Boolean)
      .join("/");
    let parsed: RawRunsIndex;
    try {
      parsed = JSON.parse(fs.readFileSync(runsJsonPath, "utf8")) as RawRunsIndex;
    } catch {
      // Skip malformed indexes — the viewer would barf on partial JSON
      // anyway, and a stale half-written file shouldn't break the
      // aggregate.
      continue;
    }
    if (typeof parsed.version === "number") version = parsed.version;
    const runs = Array.isArray(parsed.runs) ? (parsed.runs as RawRunEntry[]) : [];
    for (const entry of runs) {
      const originalRelPath = typeof entry.rel_path === "string" ? entry.rel_path : "";
      const prefixed = bucketRel.length === 0
        ? originalRelPath
        : originalRelPath.length === 0
        ? bucketRel
        : `${bucketRel}/${originalRelPath}`;
      aggregated.push({ ...entry, rel_path: prefixed });
    }
  }

  // Newest first — the sidebar's default sort.
  aggregated.sort((a, b) => {
    const ta = typeof a.started_at_unix === "number" ? a.started_at_unix : 0;
    const tb = typeof b.started_at_unix === "number" ? b.started_at_unix : 0;
    return tb - ta;
  });

  if (method === "HEAD") {
    res.end();
    return;
  }
  res.end(JSON.stringify({ version, runs: aggregated }));
}

function walkForRunsJsons(root: string): string[] {
  const out: string[] = [];
  // Iterative walk. Skips dotfile dirs and bails on individual stat
  // failures — a permission glitch on one cell shouldn't strand the
  // whole aggregate.
  const stack: string[] = [root];
  while (stack.length > 0) {
    const dir = stack.pop()!;
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      continue;
    }
    for (const entry of entries) {
      if (entry.name.startsWith(".")) continue;
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        stack.push(full);
      } else if (entry.isFile() && entry.name === "runs.json") {
        out.push(full);
      }
    }
  }
  return out;
}

// Static build. The viewer is asset-free at v1 and loads replay data
// either via the in-page file picker (still works in any deployment) or
// — when run under `vite dev` — by fetching `/replays/...` paths the
// dev plugin above serves out of `<repo>/replays/`.
export default defineConfig({
  root: ".",
  plugins: [replaysFileMount()],
  server: {
    fs: {
      // Allow the dev server to read files outside `tools/replay-viewer/`.
      // Needed because `replays/` sits at the repo root.
      allow: [path.resolve(__dirname, "../.."), __dirname],
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    target: "es2022",
  },
});
