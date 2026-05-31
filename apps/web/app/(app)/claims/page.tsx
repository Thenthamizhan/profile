import { redirect } from "next/navigation";
import { getToken, hasPerm } from "@/lib/session";
import { listClaims, type ClaimItem } from "@/lib/api";
import { Alert } from "@/components/ui";
import { SubmitClaimForm, ClaimsTable } from "./claims-client";

export const dynamic = "force-dynamic";

export default async function ClaimsPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string }>;
}) {
  const token = await getToken();
  if (!token) redirect("/login");

  if (!hasPerm(token, "claim.read")) {
    return (
      <main className="mx-auto max-w-5xl px-6 py-10">
        <p className="text-sm text-muted-foreground">
          You lack <code>claim.read</code>.
        </p>
      </main>
    );
  }

  const canRequest = hasPerm(token, "claim.request");
  const canApprove = hasPerm(token, "claim.approve");
  const canReimburse = hasPerm(token, "claim.reimburse");

  const sp = await searchParams;
  const status = sp.status ?? "";

  let items: ClaimItem[] = [];
  let error: string | null = null;
  try {
    items = await listClaims(status || undefined);
  } catch (e) {
    error = e instanceof Error ? e.message : "Failed to load claims.";
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-8">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Expense claims</h1>
        <p className="text-sm text-muted-foreground">Submit expenses and process approvals and reimbursements.</p>
      </div>

      {error && (
        <Alert tone="danger" className="mt-6">
          {error} — is the API running on <span className="font-mono">:5080</span>?
        </Alert>
      )}

      {canRequest ? (
        <section className="mt-6">
          <h2 className="mb-2 text-sm font-medium text-foreground">Submit a claim</h2>
          <SubmitClaimForm />
        </section>
      ) : (
        <p className="mt-6 text-sm text-muted-foreground">
          Read-only — you lack <code>claim.request</code>.
        </p>
      )}

      <section className="mt-8">
        <ClaimsTable items={items} canApprove={canApprove} canReimburse={canReimburse} />
      </section>
    </main>
  );
}
