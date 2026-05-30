// FF-2 — Tenant isolation: every tenant-scoped table carries tenant_id AND enables RLS.
// Owner: DSN-2 / QA-1. AOM §6, §4.4.
import { join } from 'node:path';
import { ROOT, walk, read, rel } from './_util.mjs';

export const id = 'FF-2';
export const name = 'Tenant isolation (tenant_id + RLS on tenant-scoped tables)';

// Tables that legitimately have no tenant_id: the tenant root, the GLOBAL permission catalog
// (system-wide, not per-tenant — architecture §6.3), and framework/bookkeeping tables.
const EXEMPT = new Set(['tenant', 'permission', '__ef_migrations_history', '__efmigrationshistory', 'schema_migrations']);

export function run() {
  const findings = [];
  const files = [...walk(join(ROOT, 'db'), ['.sql']), ...walk(join(ROOT, 'infra', 'migrations'), ['.sql'])];
  for (const f of files) {
    const sql = read(f);
    const r = rel(f);
    const re = /create\s+table\s+(?:if\s+not\s+exists\s+)?("?[\w.]+"?)\s*\(([\s\S]*?)\);/gi;
    let m;
    while ((m = re.exec(sql))) {
      const table = m[1].replaceAll('"', '').split('.').pop().toLowerCase();
      const body = m[2].toLowerCase();
      if (EXEMPT.has(table)) continue;
      if (!/\btenant_id\b/.test(body)) {
        findings.push(`${r}: table "${table}" has no tenant_id column`);
      }
      const rls = new RegExp(`alter\\s+table\\s+"?${table}"?\\s+enable\\s+row\\s+level\\s+security`, 'i');
      if (!rls.test(sql)) {
        findings.push(`${r}: table "${table}" never ENABLEs ROW LEVEL SECURITY`);
      }
    }
  }
  return { pass: findings.length === 0, findings };
}
