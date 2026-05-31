import { redirect } from "next/navigation";
import { getToken, hasPerm } from "@/lib/session";
import { listLeave, type LeaveRequestItem } from "@/lib/api";
import { Alert } from "@/components/ui";
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
        <p className="text-sm text-muted-foreground">
          You lack <code>leave.read</code>.
        </p>
      </main>
    );
  }

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
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Leave requests</h1>
        <p className="text-sm text-muted-foreground">Submit time off and review pending approvals.</p>
      </div>

      {error && (
        <Alert tone="danger" className="mt-6">
          {error} — is the API running on <span className="font-mono">:5080</span>?
        </Alert>
      )}

      {canRequest ? (
        <section className="mt-6">
          <h2 className="mb-2 text-sm font-medium text-foreground">Submit a request</h2>
          <SubmitLeaveForm />
        </section>
      ) : (
        <p className="mt-6 text-sm text-muted-foreground">
          Read-only — you lack <code>leave.request</code>.
        </p>
      )}

      <section className="mt-8">
        <LeaveTable items={items} canApprove={canApprove} />
      </section>
    </main>
  );
}
