import { redirect } from "next/navigation";
import { getToken, hasPerm } from "@/lib/session";
import { listClaims, type ClaimItem } from "@/lib/api";
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
        <p className="text-sm text-gray-500">You lack <code>claim.read</code>.</p>
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
      <h1 className="text-2xl font-semibold text-gray-900">Expense claims</h1>

      {error && (
        <div className="mt-6 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error} — is the API running on <span className="font-mono">:5080</span>?
        </div>
      )}

      {canRequest ? (
        <section className="mt-6">
          <h2 className="mb-2 text-sm font-medium text-gray-700">Submit a claim</h2>
          <SubmitClaimForm />
        </section>
      ) : (
        <p className="mt-6 text-sm text-gray-500">Read-only — you lack <code>claim.request</code>.</p>
      )}

      <section className="mt-8">
        <ClaimsTable items={items} canApprove={canApprove} canReimburse={canReimburse} />
      </section>
    </main>
  );
}
