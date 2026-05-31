import Link from "next/link";
import { redirect } from "next/navigation";
import { ChevronLeft } from "lucide-react";
import { getToken, hasPerm } from "@/lib/session";
import { listOffers, listInterviews, type Offer, type Interview } from "@/lib/api";
import { Alert } from "@/components/ui";
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
      <Link
        href={`/recruitment/${jobId}`}
        className="inline-flex items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ChevronLeft className="size-4" aria-hidden /> Back to board
      </Link>

      <h1 className="mt-3 text-2xl font-semibold tracking-tight text-foreground">Application</h1>
      <p className="mt-1 font-mono text-xs text-muted-foreground">{appId}</p>

      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
        {canReadOffers ? (
          <div>
            {offersError && (
              <Alert tone="danger" className="mb-2">
                {offersError}
              </Alert>
            )}
            <OffersPanel applicationId={appId} offers={offers} canWrite={canWriteOffers} />
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            You lack <code>offer.read</code>.
          </p>
        )}

        {canReadInterviews ? (
          <div>
            {interviewsError && (
              <Alert tone="danger" className="mb-2">
                {interviewsError}
              </Alert>
            )}
            <InterviewsPanel applicationId={appId} interviews={interviews} canWrite={canWriteInterviews} />
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            You lack <code>interview.read</code>.
          </p>
        )}
      </div>
    </main>
  );
}
