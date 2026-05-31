import Link from "next/link";
import { redirect } from "next/navigation";
import { getToken, hasPerm } from "@/lib/session";
import { listJobs, type Job } from "@/lib/api";

export const dynamic = "force-dynamic";

export default async function RecruitmentPage() {
  const token = await getToken();
  if (!token) redirect("/login");

  if (!hasPerm(token, "job.read")) {
    return (
      <main className="mx-auto max-w-5xl px-6 py-10">
        <p className="text-sm text-gray-500">You lack <code>job.read</code>.</p>
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
      <h1 className="text-2xl font-semibold text-gray-900">Open positions</h1>
      <p className="mt-1 text-sm text-gray-500">Select a role to open its hiring pipeline.</p>

      {error && (
        <div className="mt-6 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      <ul className="mt-6 divide-y divide-gray-100 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        {jobs.map((j) => (
          <li key={j.id}>
            <Link href={`/recruitment/${j.id}`} className="flex items-center justify-between px-5 py-4 hover:bg-gray-50">
              <div>
                <p className="font-medium text-gray-900">{j.title}</p>
                <p className="text-sm text-gray-500">
                  {j.location ?? "—"} · {j.employmentType ?? "—"}
                </p>
              </div>
              <span className="rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700">{j.status}</span>
            </Link>
          </li>
        ))}
        {jobs.length === 0 && !error && (
          <li className="px-5 py-10 text-center text-sm text-gray-400">No open positions.</li>
        )}
      </ul>
    </main>
  );
}
