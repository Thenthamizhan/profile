import { cookies } from "next/headers";

export const COOKIE = "sahahr_session";

/// Seeded dev identity (matches db/init/03_seed_dev.sql) used to prefill the dev sign-in.
export const SEED = {
  tenantId: "01900000-0000-7000-8000-0000000000a1",
  userId: "01900000-0000-7000-8000-0000000000d1",
  companyId: "01900000-0000-7000-8000-0000000000c1",
};

export async function getToken(): Promise<string | null> {
  const store = await cookies();
  return store.get(COOKIE)?.value ?? null;
}

type Claims = { tenant_id?: string; sub?: string; perm?: string | string[] };

/// Decodes (does NOT verify — the API verifies) the JWT payload to drive permission-aware UI.
export function decode(token: string): Claims {
  try {
    const payload = token.split(".")[1] ?? "";
    const json = Buffer.from(payload.replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8");
    return JSON.parse(json) as Claims;
  } catch {
    return {};
  }
}

export function permissions(token: string): string[] {
  const perm = decode(token).perm;
  return Array.isArray(perm) ? perm : perm ? [perm] : [];
}

/// Mirrors the API's dot-namespaced wildcard check (module.entity.action / module.* / *).
export function hasPerm(token: string, permission: string): boolean {
  const perms = permissions(token);
  if (perms.includes("*") || perms.includes(permission)) return true;
  const module = permission.split(".")[0];
  return perms.includes(`${module}.*`);
}
