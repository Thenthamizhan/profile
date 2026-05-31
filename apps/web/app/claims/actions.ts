"use server";

import { revalidatePath } from "next/cache";
import { submitClaim, decideClaim } from "@/lib/api";

export type ClaimState = { ok?: boolean; error?: string };

function fail(status: number, verb: string): string {
  if (status === 403) return `You don't have permission to ${verb}.`;
  if (status === 404) return "Employee not found in this tenant.";
  if (status === 409) return "That action isn't allowed (e.g. requester can't approve their own claim, or it's not in the right state).";
  return `${verb} failed (${status}).`;
}

export async function submitClaimAction(_prev: ClaimState, formData: FormData): Promise<ClaimState> {
  const employeeId = String(formData.get("employeeId") ?? "").trim();
  const category = String(formData.get("category") ?? "").trim();
  const amountRaw = String(formData.get("amount") ?? "").trim();

  if (!employeeId || !category || !amountRaw) {
    return { error: "Employee, category, and amount are required." };
  }
  const amount = Number(amountRaw);
  if (Number.isNaN(amount) || amount <= 0) return { error: "Amount must be a positive number." };

  const res = await submitClaim({
    employeeId,
    category,
    amount,
    currency: String(formData.get("currency") ?? "").trim() || null,
    description: String(formData.get("description") ?? "").trim() || null,
  });
  if (!res.ok) return { error: fail(res.status, "submit claim") };
  revalidatePath("/claims");
  return { ok: true };
}

export async function decideClaimAction(_prev: ClaimState, formData: FormData): Promise<ClaimState> {
  const id = String(formData.get("id") ?? "").trim();
  const decision = String(formData.get("decision") ?? "").trim();
  if (!id || (decision !== "approve" && decision !== "reject" && decision !== "reimburse")) {
    return { error: "Invalid decision." };
  }
  const res = await decideClaim(id, decision as "approve" | "reject" | "reimburse");
  if (!res.ok) return { error: fail(res.status, `${decision} claim`) };
  revalidatePath("/claims");
  return { ok: true };
}
