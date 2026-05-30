// FF-4 — No hard-coded statutory rates in the payroll module.
// Rates/ceilings must come from the versioned rate table (db/rate-tables), never a literal.
// Owner: IMP-2. AOM §6, §12.
import { join } from 'node:path';
import { ROOT, walk, read, rel } from './_util.mjs';

export const id = 'FF-4';
export const name = 'No hard-coded statutory rates in payroll';

export function run() {
  const findings = [];
  const base = join(ROOT, 'apps', 'api', 'src', 'modules', 'payroll');
  for (const f of walk(base, ['.cs'])) {
    if (/Tests?\.cs$/.test(f) || /\.(test|spec)\.cs$/i.test(f)) continue;
    const r = rel(f);
    read(f).split('\n').forEach((line, i) => {
      if (/^\s*\/\//.test(line)) return; // skip comments
      // decimal literals like 0.20, 1.37, 6000.00 — the shape of rates/ceilings
      const lit = line.match(/(?<![\w.])\d{1,6}\.\d+[mMdD]?(?![\w.])/);
      if (!lit) return;
      if (/version|\d+\.\d+\.\d+/.test(line)) return; // version strings, not rates
      findings.push(`${r}:${i + 1}: numeric literal "${lit[0]}" — load rates from the versioned rate table`);
    });
  }
  return { pass: findings.length === 0, findings };
}
