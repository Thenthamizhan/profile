import { redirect } from "next/navigation";
import { getToken, hasPerm } from "@/lib/session";
import { listAttendance, type AttendanceEntry } from "@/lib/api";
import { Alert } from "@/components/ui";
import { ClockForm, AttendanceTable } from "./time-client";

export const dynamic = "force-dynamic";

export default async function TimePage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string }>;
}) {
  const token = await getToken();
  if (!token) redirect("/login");

  if (!hasPerm(token, "attendance.read")) {
    return (
      <main className="mx-auto max-w-5xl px-6 py-10">
        <p className="text-sm text-muted-foreground">
          You lack <code>attendance.read</code>.
        </p>
      </main>
    );
  }

  const canClock = hasPerm(token, "attendance.clock");

  const sp = await searchParams;
  const status = sp.status ?? "";

  let items: AttendanceEntry[] = [];
  let error: string | null = null;
  try {
    items = await listAttendance(undefined, status || undefined);
  } catch (e) {
    error = e instanceof Error ? e.message : "Failed to load attendance.";
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-8">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Time &amp; Attendance</h1>
        <p className="text-sm text-muted-foreground">Clock employees in and out; hours are computed on clock-out.</p>
      </div>

      {error && (
        <Alert tone="danger" className="mt-6">
          {error} — is the API running on <span className="font-mono">:5080</span>?
        </Alert>
      )}

      {canClock ? (
        <section className="mt-6">
          <h2 className="mb-2 text-sm font-medium text-foreground">Clock in / out</h2>
          <ClockForm />
        </section>
      ) : (
        <p className="mt-6 text-sm text-muted-foreground">
          Read-only — you lack <code>attendance.clock</code>.
        </p>
      )}

      <section className="mt-8">
        <AttendanceTable items={items} />
      </section>
    </main>
  );
}
