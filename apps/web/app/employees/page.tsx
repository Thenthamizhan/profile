import { redirect } from "next/navigation";
import { decode, getToken, hasPerm, permissions, SEED } from "@/lib/session";
import { listEmployees, type Employee } from "@/lib/api";
import { CreateForm } from "./create-form";
import { EmployeeTable } from "./employee-table";
import { logout } from "./actions";

export const dynamic = "force-dynamic";

export default async function EmployeesPage() {
  const token = await getToken();
  if (!token) redirect("/login");

  const claims = decode(token);
  const canWrite = hasPerm(token, "employee.write");
  const canDelete = hasPerm(token, "employee.delete");

  let employees: Employee[] = [];
  let error: string | null = null;
  try {
    employees = await listEmployees();
  } catch (e) {
    error = e instanceof Error ? e.message : "Failed to load employees.";
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-10">
      <header className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Employees</h1>
          <p className="mt-1 text-sm text-gray-500">
            Tenant <span className="font-mono text-xs">{claims.tenant_id}</span> ·{" "}
            {permissions(token).length} permission(s)
          </p>
        </div>
        <form action={logout}>
          <button className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50">
            Sign out
          </button>
        </form>
      </header>

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
        <p className="mb-2 text-xs text-gray-400">Click a row to view, edit, or delete.</p>
        <EmployeeTable employees={employees} canWrite={canWrite} canDelete={canDelete} />
      </section>
    </main>
  );
}
