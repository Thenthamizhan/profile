import Link from "next/link";
import { redirect } from "next/navigation";
import { decode, getToken, hasPerm, permissions, SEED } from "@/lib/session";
import { listEmployees, type Employee } from "@/lib/api";
import { CreateForm } from "./create-form";
import { EmployeeTable } from "./employee-table";
import { FilterBar } from "./filter-bar";
import { TopNav } from "../nav";

export const dynamic = "force-dynamic";

const LIMIT = 20;

export default async function EmployeesPage({
  searchParams,
}: {
  searchParams: Promise<{ search?: string; status?: string; cursor?: string }>;
}) {
  const token = await getToken();
  if (!token) redirect("/login");

  const claims = decode(token);
  const canWrite = hasPerm(token, "employee.write");
  const canDelete = hasPerm(token, "employee.delete");

  const sp = await searchParams;
  const search = sp.search ?? "";
  const status = sp.status ?? "";

  let employees: Employee[] = [];
  let nextCursor: string | null = null;
  let error: string | null = null;
  try {
    const page = await listEmployees({ search, status, cursor: sp.cursor, limit: LIMIT });
    employees = page.items;
    nextCursor = page.nextCursor;
  } catch (e) {
    error = e instanceof Error ? e.message : "Failed to load employees.";
  }

  const baseQs = new URLSearchParams();
  if (search) baseQs.set("search", search);
  if (status) baseQs.set("status", status);
  const nextHref = nextCursor
    ? `/employees?${new URLSearchParams({ ...Object.fromEntries(baseQs), cursor: nextCursor })}`
    : null;
  const firstHref = `/employees${baseQs.toString() ? `?${baseQs}` : ""}`;
  const onFirstPage = !sp.cursor;

  return (
    <main className="mx-auto max-w-5xl px-6 py-8">
      <TopNav
        tenantId={claims.tenant_id}
        permCount={permissions(token).length}
        canSeePeople
        canSeeRecruitment={hasPerm(token, "job.read")}
        active="people"
      />
      <h1 className="mt-6 text-2xl font-semibold text-gray-900">Employees</h1>

      {error && (
        <div className="mt-6 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error} — is the API running on <span className="font-mono">:5080</span>?
        </div>
      )}

      {canWrite ? (
        <section className="mt-6">
          <h2 className="mb-2 text-sm font-medium text-gray-700">Add employee</h2>
          <CreateForm companyId={SEED.companyId} />
        </section>
      ) : (
        <p className="mt-6 text-sm text-gray-500">Read-only — you lack <code>employee.write</code>.</p>
      )}

      <section className="mt-8">
        <div className="mb-3 flex items-center justify-between gap-4">
          <FilterBar search={search} status={status} />
          <p className="shrink-0 text-xs text-gray-400">Click a row to view, edit, or delete.</p>
        </div>

        <EmployeeTable employees={employees} canWrite={canWrite} canDelete={canDelete} />

        <nav className="mt-4 flex items-center justify-between text-sm">
          <span className="text-gray-400">
            {employees.length} shown{!onFirstPage ? " (paged)" : ""}
          </span>
          <div className="flex gap-2">
            {!onFirstPage && (
              <Link href={firstHref} className="rounded-md border border-gray-300 px-3 py-1.5 text-gray-700 hover:bg-gray-50">
                ← First page
              </Link>
            )}
            {nextHref ? (
              <Link href={nextHref} className="rounded-md border border-gray-300 px-3 py-1.5 text-gray-700 hover:bg-gray-50">
                Next →
              </Link>
            ) : (
              <span className="rounded-md border border-gray-100 px-3 py-1.5 text-gray-300">Next →</span>
            )}
          </div>
        </nav>
      </section>
    </main>
  );
}
