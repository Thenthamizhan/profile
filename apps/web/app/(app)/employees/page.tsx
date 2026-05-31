import Link from "next/link";
import { redirect } from "next/navigation";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { getToken, hasPerm, SEED } from "@/lib/session";
import { listEmployees, type Employee } from "@/lib/api";
import { Alert, Button } from "@/components/ui";
import { CreateForm } from "./create-form";
import { EmployeeTable } from "./employee-table";
import { FilterBar } from "./filter-bar";

export const dynamic = "force-dynamic";

const LIMIT = 20;

export default async function EmployeesPage({
  searchParams,
}: {
  searchParams: Promise<{ search?: string; status?: string; cursor?: string }>;
}) {
  const token = await getToken();
  if (!token) redirect("/login");

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
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Employees</h1>
        <p className="text-sm text-muted-foreground">Directory of people in your organisation.</p>
      </div>

      {error && (
        <Alert tone="danger" className="mt-6">
          {error} — is the API running on <span className="font-mono">:5080</span>?
        </Alert>
      )}

      {canWrite ? (
        <section className="mt-6">
          <h2 className="mb-2 text-sm font-medium text-foreground">Add employee</h2>
          <CreateForm companyId={SEED.companyId} />
        </section>
      ) : (
        <p className="mt-6 text-sm text-muted-foreground">
          Read-only — you lack <code>employee.write</code>.
        </p>
      )}

      <section className="mt-8">
        <div className="mb-3 flex items-center justify-between gap-4">
          <FilterBar search={search} status={status} />
          <p className="shrink-0 text-xs text-muted-foreground">Click a row to view, edit, or delete.</p>
        </div>

        <EmployeeTable employees={employees} canWrite={canWrite} canDelete={canDelete} />

        <nav className="mt-4 flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            {employees.length} shown{!onFirstPage ? " (paged)" : ""}
          </span>
          <div className="flex gap-2">
            {!onFirstPage && (
              <Button asChild variant="secondary" size="sm">
                <Link href={firstHref}>
                  <ChevronLeft /> First page
                </Link>
              </Button>
            )}
            {nextHref ? (
              <Button asChild variant="secondary" size="sm">
                <Link href={nextHref}>
                  Next <ChevronRight />
                </Link>
              </Button>
            ) : (
              <Button variant="secondary" size="sm" disabled>
                Next <ChevronRight />
              </Button>
            )}
          </div>
        </nav>
      </section>
    </main>
  );
}
