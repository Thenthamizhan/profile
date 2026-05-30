import Link from "next/link";
import { redirect } from "next/navigation";
import { decode, getToken, hasPerm, permissions } from "@/lib/session";
import { listOffers, listInterviews, type Offer, type Interview } from "@/lib/api";
import { TopNav } from "../../../../nav";
import { OffersPanel, InterviewsPanel } from "./panels";

export const dynamic = "force-dynamic";

export default async function ApplicationDetailPage({
  params,
}: {
  params: Promise<{ jobId: string; appId: string }>;
}) {
  const token = await getToken();
  if (!token) redirect("/login");

  const { jobId, appId } = await params;
  const claims = decode(token);

  const canReadOffers = hasPerm(token, "offer.read");
  const canWriteOffers = hasPerm(token, "offer.write");
  const canReadInterviews = hasPerm(token, "interview.read");
  const canWriteInterviews = hasPerm(token, "interview.write");

  // Fetch only what the user may read; tolerate per-panel failure independently.
  let offers: Offer[] = [];
  let offersError: string | null = null;
  if (canReadOffers) {
    try { offers = await listOffers(appId); }
    catch (e) { offersError = e instanceof Error ? e.message : "Failed to load offers."; }
  }

  let interviews: Interview[] = [];
  let interviewsError: string | null = null;
  if (canReadInterviews) {
    try { interviews = await listInterviews(appId); }
    catch (e) { interviewsError = e instanceof Error ? e.message : "Failed to load interviews."; }
  }

  return (
    <main className="mx-auto max-w-4xl px-6 py-8">
      <TopNav
        tenantId={claims.tenant_id}
        permCount={permissions(token).length}
        canSeePeople={hasPerm(token, "employee.read")}
        canSeeRecruitment
        active="recruitment"
      />

      <div className="mt-6 flex items-center gap-3 text-sm text-gray-500">
        <Link href={`/recruitment/${jobId}`} className="hover:text-gray-700 hover:underline">← Back to board</Link>
      </div>

      <h1 className="mt-3 text-2xl font-semibold text-gray-900">Application</h1>
      <p className="mt-1 font-mono text-xs text-gray-500">{appId}</p>

      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
        {canReadOffers ? (
          <div>
            {offersError && <p className="mb-2 text-sm text-red-600">{offersError}</p>}
            <OffersPanel applicationId={appId} offers={offers} canWrite={canWriteOffers} />
          </div>
        ) : (
          <p className="text-sm text-gray-400">You lack <code>offer.read</code>.</p>
        )}

        {canReadInterviews ? (
          <div>
            {interviewsError && <p className="mb-2 text-sm text-red-600">{interviewsError}</p>}
            <InterviewsPanel applicationId={appId} interviews={interviews} canWrite={canWriteInterviews} />
          </div>
        ) : (
          <p className="text-sm text-gray-400">You lack <code>interview.read</code>.</p>
        )}
      </div>
    </main>
  );
}
