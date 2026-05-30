#!/usr/bin/env node
// Runs every architectural fitness function and exits non-zero if any fail.
// Wired into CI (.github/workflows/ci.yml) and runnable locally via `pnpm ff`.
// AOM §6 — these are the executable invariants that gate promotion.
import * as ff01 from './ff-01-module-boundary.mjs';
import * as ff02 from './ff-02-tenant-isolation.mjs';
import * as ff04 from './ff-04-no-hardcoded-rates.mjs';
import * as ff05 from './ff-05-money-type.mjs';
import * as ff12 from './ff-12-migration-reversibility.mjs';

// Statically-checkable now. The rest (FF-3,6,7,8,9,10,11,13,14,15,16,17) require
// running code / CI services and are added as their subjects land — see scripts/fitness/README.md.
const checks = [ff01, ff02, ff04, ff05, ff12];

const pad = (s, n) => String(s).padEnd(n);
const results = checks.map((c) => {
  try {
    return { c, r: c.run() };
  } catch (e) {
    return { c, r: { pass: false, findings: [`check threw: ${e.message}`] } };
  }
});

console.log('\nArchitectural fitness functions (AOM §6)\n');
console.log(pad('ID', 7) + pad('STATUS', 9) + pad('CHECK', 52) + 'FINDINGS');
console.log('-'.repeat(80));
for (const { c, r } of results) {
  console.log(pad(c.id, 7) + pad(r.pass ? 'PASS' : 'FAIL', 9) + pad(c.name, 52) + r.findings.length);
}

for (const { c, r } of results) {
  if (r.findings.length) {
    console.log(`\n${c.id} — ${c.name}:`);
    for (const f of r.findings.slice(0, 50)) console.log('  - ' + f);
    if (r.findings.length > 50) console.log(`  … and ${r.findings.length - 50} more`);
  }
}

const failed = results.filter(({ r }) => !r.pass).length;
console.log(`\n${failed ? `✗ ${failed} fitness function(s) failed` : '✓ all fitness functions passed'}\n`);
process.exit(failed ? 1 : 0);
