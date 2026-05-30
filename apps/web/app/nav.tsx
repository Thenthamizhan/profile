import Link from "next/link";
import { logout } from "./employees/actions";

/// Top nav shared by the authenticated pages. Permission-aware: a section only appears if the
/// user holds the relevant read permission.
export function TopNav({
  tenantId,
  permCount,
  canSeePeople,
  canSeeRecruitment,
  canSeeLeave = false,
  active,
}: {
  tenantId?: string;
  permCount: number;
  canSeePeople: boolean;
  canSeeRecruitment: boolean;
  canSeeLeave?: boolean;
  active: "people" | "recruitment" | "leave";
}) {
  const link = (href: string, label: string, isActive: boolean) => (
    <Link
      href={href}
      className={`rounded-md px-3 py-1.5 text-sm font-medium ${
        isActive ? "bg-gray-900 text-white" : "text-gray-600 hover:bg-gray-100"
      }`}
    >
      {label}
    </Link>
  );

  return (
    <header className="flex items-center justify-between border-b border-gray-200 pb-4">
      <div className="flex items-center gap-6">
        <span className="text-lg font-semibold text-gray-900">SahaHR</span>
        <nav className="flex gap-1">
          {canSeePeople && link("/employees", "Employees", active === "people")}
          {canSeeRecruitment && link("/recruitment", "Recruitment", active === "recruitment")}
          {canSeeLeave && link("/leave", "Leave", active === "leave")}
        </nav>
      </div>
      <div className="flex items-center gap-4">
        <span className="hidden text-xs text-gray-400 sm:inline">
          Tenant <span className="font-mono">{tenantId?.slice(0, 8)}…</span> · {permCount} perms
        </span>
        <form action={logout}>
          <button className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50">
            Sign out
          </button>
        </form>
      </div>
    </header>
  );
}
