// Shared helpers for architectural fitness-function checks (no external deps — Node builtins only).
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

const SKIP_DIRS = new Set(['node_modules', 'bin', 'obj', 'dist', '.next', '.git', '.turbo', 'TestResults']);

/** Recursively list files under absDir, optionally filtered by extension suffixes. */
export function walk(absDir, exts) {
  const out = [];
  if (!existsSync(absDir)) return out;
  for (const entry of readdirSync(absDir, { withFileTypes: true })) {
    if (SKIP_DIRS.has(entry.name)) continue;
    const p = join(absDir, entry.name);
    if (entry.isDirectory()) out.push(...walk(p, exts));
    else if (!exts || exts.some((e) => entry.name.endsWith(e))) out.push(p);
  }
  return out;
}

export function read(f) {
  return readFileSync(f, 'utf8');
}

export function exists(p) {
  return existsSync(p);
}

/** Repo-relative, forward-slashed path for stable reporting. */
export function rel(p) {
  return p.slice(ROOT.length + 1).replaceAll('\\', '/');
}
