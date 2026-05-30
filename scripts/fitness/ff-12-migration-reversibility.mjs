// FF-12 — Migration reversibility: every migration has a rollback (EF Down() or paired *.down.sql).
// Owner: IMP-6. AOM §6, §16.3.
import { join } from 'node:path';
import { ROOT, walk, read, rel, exists } from './_util.mjs';

export const id = 'FF-12';
export const name = 'Migration reversibility (every migration has a rollback)';

export function run() {
  const findings = [];

  // EF Core migrations: a class with Up() must also define Down().
  for (const f of walk(join(ROOT, 'apps', 'api'), ['.cs'])) {
    if (!/migrations?/i.test(rel(f))) continue;
    const s = read(f);
    if (/override\s+void\s+Up\s*\(/.test(s) && !/override\s+void\s+Down\s*\(/.test(s)) {
      findings.push(`${rel(f)}: migration defines Up() but no Down()`);
    }
  }

  // Raw SQL migrations: each *.up.sql needs a sibling *.down.sql.
  for (const f of walk(join(ROOT, 'infra', 'migrations'), ['.sql'])) {
    if (f.endsWith('.up.sql')) {
      const down = f.replace(/\.up\.sql$/, '.down.sql');
      if (!exists(down)) findings.push(`${rel(f)}: missing paired down migration (${rel(down)})`);
    }
  }

  return { pass: findings.length === 0, findings };
}
