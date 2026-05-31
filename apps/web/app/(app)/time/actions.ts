"use server";

import { revalidatePath } from "next/cache";
import { clockIn, clockOut } from "@/lib/api";

export type AttendanceState = { ok?: boolean; error?: string };

/// One action drives both transitions; the submit button supplies intent=in|out.
export async function clockAction(_prev: AttendanceState, formData: FormData): Promise<AttendanceState> {
  const employeeId = String(formData.get("employeeId") ?? "").trim();
  const notes = String(formData.get("notes") ?? "").trim() || null;
  const intent = String(formData.get("intent") ?? "in");
  if (!employeeId) return { error: "Employee ID is required." };

  const res = intent === "out" ? await clockOut(employeeId, notes) : await clockIn(employeeId, notes);
  if (!res.ok) {
    if (res.status === 404) return { error: "Employee not found in this tenant." };
    if (res.status === 409)
      return { error: intent === "out" ? "Employee is not clocked in." : "Employee is already clocked in." };
    if (res.status === 403) return { error: "You don't have permission to clock attendance." };
    return { error: `Clock-${intent} failed (${res.status}).` };
  }

  revalidatePath("/time");
  return { ok: true };
}
