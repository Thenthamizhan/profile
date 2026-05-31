import { redirect } from "next/navigation";
import { LogOut } from "lucide-react";
import { decode, getToken, hasPerm, permissions } from "@/lib/session";
import { NavLinks, type NavItem } from "@/components/shell/nav-links";
import { Button } from "@/components/ui";
import { logout } from "./employees/actions";

export const dynamic = "force-dynamic";

/// Authenticated app shell. Scoped to the (app) route group so /login and / stay chrome-free.
/// Renders once and persists across navigations within the group — the sidebar does not re-mount.
export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const token = await getToken();
  if (!token) redirect("/login");

  const claims = decode(token);
  const perms = permissions(token);

  const items: NavItem[] = [];
  if (hasPerm(token, "employee.read")) items.push({ href: "/employees", label: "Employees", icon: "employees" });
  if (hasPerm(token, "job.read")) items.push({ href: "/recruitment", label: "Recruitment", icon: "recruitment" });
  if (hasPerm(token, "leave.read")) items.push({ href: "/leave", label: "Leave", icon: "leave" });
  if (hasPerm(token, "claim.read")) items.push({ href: "/claims", label: "Claims", icon: "claims" });
  if (hasPerm(token, "attendance.read")) items.push({ href: "/time", label: "Time", icon: "time" });

  return (
    <div className="flex min-h-screen bg-background">
      {/* Desktop sidebar */}
      <aside className="hidden w-64 shrink-0 flex-col border-r border-border bg-surface md:flex">
        <div className="flex h-14 items-center gap-2 border-b border-border px-5">
          <span className="text-base font-semibold tracking-tight text-foreground">SahaHR</span>
        </div>
        <div className="flex-1 overflow-y-auto py-4">
          <NavLinks items={items} />
        </div>
        <div className="border-t border-border px-5 py-3 text-xs text-muted-foreground">
          Tenant <span className="font-mono">{claims.tenant_id?.slice(0, 8)}…</span>
          <br />
          {perms.length} permission{perms.length === 1 ? "" : "s"}
        </div>
      </aside>

      {/* Main column */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center justify-between gap-4 border-b border-border bg-surface px-6">
          <span className="text-sm font-semibold text-foreground md:hidden">SahaHR</span>
          <form action={logout} className="ml-auto">
            <Button variant="secondary" size="sm" type="submit">
              <LogOut className="size-4" aria-hidden />
              Sign out
            </Button>
          </form>
        </header>

        {/* Mobile nav strip */}
        <div className="border-b border-border bg-surface md:hidden">
          <NavLinks items={items} orientation="horizontal" />
        </div>

        <div className="flex-1 overflow-y-auto">{children}</div>
      </div>
    </div>
  );
}
