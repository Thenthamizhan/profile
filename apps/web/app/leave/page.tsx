import { redirect } from "next/navigation";
import { decode, getToken, hasPerm, permissions } from "@/lib/session";
import { listLeave, type LeaveRequestItem } from "@/lib/api";
import { TopNav } from "../nav";
import { SubmitLeaveForm, LeaveTable } from "./leave-client";

export const dynamic = "force-dynamic";

export default async function LeavePage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string }>;
}) {
  const token = await getToken();
  if (!token) redirect("/login");

  if (!hasPerm(token, "leave.read")) {
    return (
      <main className="mx-auto max-w-5xl px-6 py-10">
        <p className="text-sm text-gray-500">You lack <code>leave.read</code>.</p>
      </main>
    );
  }

  const claims = decode(token);
  const canRequest = hasPerm(token, "leave.request");
  const canApprove = hasPerm(token, "leave.approve");

  const sp = await searchParams;
  const status = sp.status ?? "";

  let items: LeaveRequestItem[] = [];
  let error: string | null = null;
  try {
    items = await listLeave(status || undefined);
  } catch (e) {
    error = e instanceof Error ? e.message : "Failed to load leave requests.";
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-8">
      <TopNav
        tenantId={claims.tenant_id}
        permCount={permissions(token).length}
        canSeePeople={hasPerm(token, "employee.read")}
        canSeeRecruitment={hasPerm(token, "job.read")}
        canSeeLeave
        active="leave"
      />
      <h1 className="mt-6 text-2xl font-semibold text-gray-900">Leave requests</h1>

      {error && (
        <div className="mt-6 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error} — is the API running on <span className="font-mono">:5080</span>?
        </div>
      )}

      {canRequest ? (
        <section className="mt-6">
          <h2 className="mb-2 text-sm font-medium text-gray-700">Submit a request</h2>
          <SubmitLeaveForm />
        </section>
      ) : (
        <p className="mt-6 text-sm text-gray-500">Read-only — you lack <code>leave.request</code>.</p>
      )}

      <section className="mt-8">
        <LeaveTable items={items} canApprove={canApprove} />
      </section>
    </main>
  );
}
