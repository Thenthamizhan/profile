"use server";

import { revalidatePath } from "next/cache";
import { submitLeave, decideLeave } from "@/lib/api";

export type LeaveState = { ok?: boolean; error?: string };

function fail(status: number, verb: string): string {
  if (status === 403) return `You don't have permission to ${verb}.`;
  if (status === 404) return "Employee not found in this tenant.";
  if (status === 409) return "That action isn't allowed (e.g. requester can't approve their own leave, or it's already decided).";
  return `${verb} failed (${status}).`;
}

export async function submitLeaveAction(_prev: LeaveState, formData: FormData): Promise<LeaveState> {
  const employeeId = String(formData.get("employeeId") ?? "").trim();
  const leaveType = String(formData.get("leaveType") ?? "").trim();
  const startDate = String(formData.get("startDate") ?? "").trim();
  const endDate = String(formData.get("endDate") ?? "").trim();
  const daysRaw = String(formData.get("days") ?? "").trim();

  if (!employeeId || !leaveType || !startDate || !endDate || !daysRaw) {
    return { error: "Employee, type, dates, and days are all required." };
  }
  const days = Number(daysRaw);
  if (Number.isNaN(days) || days <= 0) return { error: "Days must be a positive number." };

  const res = await submitLeave({
    employeeId,
    leaveType,
    startDate,
    endDate,
    days,
    reason: String(formData.get("reason") ?? "").trim() || null,
  });
  if (!res.ok) return { error: fail(res.status, "submit leave") };
  revalidatePath("/leave");
  return { ok: true };
}

export async function decideLeaveAction(_prev: LeaveState, formData: FormData): Promise<LeaveState> {
  const id = String(formData.get("id") ?? "").trim();
  const decision = String(formData.get("decision") ?? "").trim();
  if (!id || (decision !== "approve" && decision !== "reject")) return { error: "Invalid decision." };

  const res = await decideLeave(id, decision);
  if (!res.ok) return { error: fail(res.status, `${decision} leave`) };
  revalidatePath("/leave");
  return { ok: true };
}
