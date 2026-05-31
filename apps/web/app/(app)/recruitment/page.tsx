import Link from "next/link";
import { redirect } from "next/navigation";
import { ChevronRight } from "lucide-react";
import { getToken, hasPerm } from "@/lib/session";
import { listJobs, type Job } from "@/lib/api";
import { Alert, Badge } from "@/components/ui";

export const dynamic = "force-dynamic";

export default async function RecruitmentPage() {
  const token = await getToken();
  if (!token) redirect("/login");

  if (!hasPerm(token, "job.read")) {
    return (
      <main className="mx-auto max-w-5xl px-6 py-10">
        <p className="text-sm text-muted-foreground">
          You lack <code>job.read</code>.
        </p>
      </main>
    );
  }

  let jobs: Job[] = [];
  let error: string | null = null;
  try {
    jobs = await listJobs();
  } catch (e) {
    error = e instanceof Error ? e.message : "Failed to load jobs.";
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-8">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Open positions</h1>
        <p className="text-sm text-muted-foreground">Select a role to open its hiring pipeline.</p>
      </div>

      {error && (
        <Alert tone="danger" className="mt-6">
          {error}
        </Alert>
      )}

      <ul className="mt-6 divide-y divide-border overflow-hidden rounded-[var(--radius-app)] border border-border bg-surface shadow-sm">
        {jobs.map((j) => (
          <li key={j.id}>
            <Link
              href={`/recruitment/${j.id}`}
              className="flex items-center justify-between gap-4 px-5 py-4 transition-colors hover:bg-surface-muted"
            >
              <div>
                <p className="font-medium text-foreground">{j.title}</p>
                <p className="text-sm text-muted-foreground">
                  {j.location ?? "—"} · {j.employmentType ?? "—"}
                </p>
              </div>
              <div className="flex items-center gap-3">
                <Badge tone="success">{j.status}</Badge>
                <ChevronRight className="size-4 text-muted-foreground" aria-hidden />
              </div>
            </Link>
          </li>
        ))}
        {jobs.length === 0 && !error && (
          <li className="px-5 py-10 text-center text-sm text-muted-foreground">No open positions.</li>
        )}
      </ul>
    </main>
  );
}
