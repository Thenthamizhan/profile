// FF-5 — Money type safety: monetary columns/properties must be exact (numeric/decimal), never float.
// Owner: DSN-2. AOM §6, §6.1.
import { join } from 'node:path';
import { ROOT, walk, read, rel } from './_util.mjs';

export const id = 'FF-5';
export const name = 'Money type safety (no float/double on monetary fields)';

const MONEY = /(amount|salary|basic|gross|\bnet\b|cpf|wage|payable|\btax\b|deduction|allowance|bonus|\bprice\b|\bcost\b)/i;

// Strip trailing line comments so prose (e.g. "-- money: numeric only") never trips the check.
const stripSql = (line) => line.split('--')[0];
const stripCs = (line) => line.split('//')[0];

export function run() {
  const findings = [];

  // SQL: monetary columns must not be float/double precision/real/money.
  for (const f of [...walk(join(ROOT, 'db'), ['.sql']), ...walk(join(ROOT, 'infra', 'migrations'), ['.sql'])]) {
    const r = rel(f);
    read(f).split('\n').forEach((raw, i) => {
      const line = stripSql(raw);
      if (MONEY.test(line) && /\b(float|double\s+precision|real|money)\b/i.test(line)) {
        findings.push(`${r}:${i + 1}: monetary column uses float/double/money — use numeric(18,4)`);
      }
    });
  }

  // C#: monetary properties must not be double/float.
  for (const f of walk(join(ROOT, 'apps', 'api'), ['.cs'])) {
    const r = rel(f);
    read(f).split('\n').forEach((raw, i) => {
      const line = stripCs(raw);
      if (MONEY.test(line) && /\bpublic\b/.test(line) && /\b(double|float)\b/.test(line)) {
        findings.push(`${r}:${i + 1}: monetary property uses double/float — use decimal`);
      }
    });
  }

  return { pass: findings.length === 0, findings };
}
