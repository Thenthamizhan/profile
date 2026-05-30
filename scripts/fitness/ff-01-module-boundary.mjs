// FF-1 — Module boundary: no module imports another module's internals.
// Cross-module references are allowed only via shared sub-namespaces (Contracts/Abstractions/Events).
// Owner: QA-2 (authored under STR-3). AOM §6.
import { join } from 'node:path';
import { ROOT, walk, read, rel } from './_util.mjs';

export const id = 'FF-1';
export const name = 'Module boundary (no cross-module internal imports)';

const SHARED = new Set(['Contracts', 'Abstractions', 'Events']);

export function run() {
  const findings = [];
  const base = join(ROOT, 'apps', 'api', 'src', 'modules');
  for (const f of walk(base, ['.cs'])) {
    const r = rel(f);
    const m = r.match(/\/modules\/([^/]+)\//i);
    if (!m) continue;
    // dir is the project name e.g. "SahaHR.Modules.People" -> short module name "people"
    const self = m[1].split('.').pop().toLowerCase();
    const lines = read(f).split('\n');
    lines.forEach((line, i) => {
      const u = line.match(/^\s*using\s+SahaHR\.Modules\.([A-Za-z0-9]+)(?:\.([A-Za-z0-9]+))?/);
      if (!u) return;
      const other = u[1].toLowerCase();
      const sub = u[2] || '';
      if (other !== self && !SHARED.has(sub)) {
        findings.push(`${r}:${i + 1}: imports SahaHR.Modules.${u[1]}${sub ? '.' + sub : ''} — cross-module internal reference`);
      }
    });
  }
  return { pass: findings.length === 0, findings };
}
