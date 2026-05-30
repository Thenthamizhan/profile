#!/usr/bin/env node
// Minimal, dependency-free SQL migration runner (pays DEBT-002).
//
// Applies ordered NNNN_*.up.sql / rolls back NNNN_*.down.sql from infra/migrations/, tracking
// applied versions in a schema_migrations table. Each migration runs inside a transaction.
//
// It shells out to `psql`, which it locates as either a local binary or via the project's
// postgres:16-alpine Docker image (same pattern the rest of the repo uses). Pass an OWNER
// connection (migrations run DDL); the app role must not own tables.
//
// Usage:
//   node scripts/migrate/run.mjs <up|down|status> "<postgres connection URI>"
import { readdirSync, readFileSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const DIR = join(ROOT, "infra", "migrations");

const [cmd, conn] = process.argv.slice(2);
if (!cmd || !["up", "down", "status"].includes(cmd) || (cmd !== "status" && !conn) ) {
  console.error('usage: node scripts/migrate/run.mjs <up|down|status> "<postgres uri>"');
  process.exit(2);
}
const CONN = conn ?? process.env.DATABASE_URL;
if (!CONN) { console.error("no connection URI provided"); process.exit(2); }

// --- psql invocation: prefer local psql, else docker run postgres:16-alpine ---
function psqlRunner() {
  try {
    execFileSync("psql", ["--version"], { stdio: "ignore" });
    return (args, input) => execFileSync("psql", [CONN, ...args], { input, encoding: "utf8" });
  } catch {
    // Docker fallback; rewrite localhost so the container can reach the host.
    const connForDocker = CONN.replace("@localhost", "@host.docker.internal").replace("@127.0.0.1", "@host.docker.internal");
    return (args, input) =>
      execFileSync(
        "docker",
        ["run", "--rm", "-i", "--add-host", "host.docker.internal:host-gateway", "postgres:16-alpine",
         "psql", connForDocker, ...args],
        { input, encoding: "utf8" },
      );
  }
}
const psql = psqlRunner();

const sql = (text) => psql(["-v", "ON_ERROR_STOP=1", "-q", "-t", "-A", "-c", text]).trim();
const sqlFile = (text) => psql(["-v", "ON_ERROR_STOP=1", "-q"], text);

function ensureTable() {
  sql(`CREATE TABLE IF NOT EXISTS schema_migrations (
         version text PRIMARY KEY,
         applied_at timestamptz NOT NULL DEFAULT now()
       )`);
}

function migrations() {
  if (!existsSync(DIR)) return [];
  const ups = readdirSync(DIR).filter((f) => f.endsWith(".up.sql")).sort();
  return ups.map((f) => ({
    version: f.replace(/\.up\.sql$/, ""),
    up: join(DIR, f),
    down: join(DIR, f.replace(/\.up\.sql$/, ".down.sql")),
  }));
}

function applied() {
  ensureTable();
  const out = sql("SELECT version FROM schema_migrations ORDER BY version");
  return new Set(out ? out.split("\n").map((s) => s.trim()).filter(Boolean) : []);
}

function runInTx(body) {
  // Wrap the migration body + bookkeeping in a single transaction.
  sqlFile(`BEGIN;\n${body}\nCOMMIT;`);
}

if (cmd === "status") {
  const done = applied();
  for (const m of migrations()) console.log(`${done.has(m.version) ? "applied" : "pending"}  ${m.version}`);
  process.exit(0);
}

if (cmd === "up") {
  const done = applied();
  const pending = migrations().filter((m) => !done.has(m.version));
  if (pending.length === 0) { console.log("nothing to apply; up to date."); process.exit(0); }
  for (const m of pending) {
    process.stdout.write(`applying ${m.version} … `);
    const body = readFileSync(m.up, "utf8");
    runInTx(`${body}\nINSERT INTO schema_migrations (version) VALUES ('${m.version}');`);
    console.log("ok");
  }
  process.exit(0);
}

if (cmd === "down") {
  const done = applied();
  const last = migrations().filter((m) => done.has(m.version)).pop();
  if (!last) { console.log("nothing to roll back."); process.exit(0); }
  if (!existsSync(last.down)) { console.error(`no down migration for ${last.version}`); process.exit(1); }
  process.stdout.write(`rolling back ${last.version} … `);
  const body = readFileSync(last.down, "utf8");
  runInTx(`${body}\nDELETE FROM schema_migrations WHERE version = '${last.version}';`);
  console.log("ok");
  process.exit(0);
}
