"use server";

import { revalidatePath } from "next/cache";
import {
  createOffer,
  sendOffer,
  respondOffer,
  scheduleInterview,
  submitScorecard,
  type CompetencyScore,
} from "@/lib/api";

export type ActionState = { ok?: boolean; error?: string };

function fail(status: number, verb: string): string {
  if (status === 403) return `You don't have permission to ${verb}.`;
  if (status === 404) return "Not found (it may have been removed).";
  if (status === 409) return "That action isn't allowed in the current state.";
  return `${verb} failed (${status}).`;
}

function revalidate(): void {
  revalidatePath("/recruitment/[jobId]/applications/[appId]", "page");
}

export async function createOfferAction(_prev: ActionState, formData: FormData): Promise<ActionState> {
  const applicationId = String(formData.get("applicationId") ?? "").trim();
  const salaryRaw = String(formData.get("salary") ?? "").trim();
  const currency = String(formData.get("currency") ?? "").trim() || null;
  if (!applicationId) return { error: "Missing application." };

  const salary = salaryRaw ? Number(salaryRaw) : null;
  if (salaryRaw && (Number.isNaN(salary) || salary! < 0)) return { error: "Salary must be a non-negative number." };

  const res = await createOffer(applicationId, { salary, currency });
  if (!res.ok) return { error: fail(res.status, "create offers") };
  revalidate();
  return { ok: true };
}

export async function sendOfferAction(_prev: ActionState, formData: FormData): Promise<ActionState> {
  const offerId = String(formData.get("offerId") ?? "").trim();
  if (!offerId) return { error: "Missing offer." };
  const res = await sendOffer(offerId);
  if (!res.ok) return { error: fail(res.status, "send the offer") };
  revalidate();
  return { ok: true };
}

export async function respondOfferAction(_prev: ActionState, formData: FormData): Promise<ActionState> {
  const offerId = String(formData.get("offerId") ?? "").trim();
  const decision = String(formData.get("decision") ?? "").trim();
  if (!offerId || (decision !== "accepted" && decision !== "declined")) return { error: "Invalid offer response." };
  const res = await respondOffer(offerId, decision);
  if (!res.ok) return { error: fail(res.status, "record the response") };
  revalidate();
  return { ok: true };
}

export async function scheduleInterviewAction(_prev: ActionState, formData: FormData): Promise<ActionState> {
  const applicationId = String(formData.get("applicationId") ?? "").trim();
  const scheduledAt = String(formData.get("scheduledAt") ?? "").trim() || null;
  if (!applicationId) return { error: "Missing application." };
  const res = await scheduleInterview(applicationId, { scheduledAt, interviewers: [] });
  if (!res.ok) return { error: fail(res.status, "schedule the interview") };
  revalidate();
  return { ok: true };
}

export async function submitScorecardAction(_prev: ActionState, formData: FormData): Promise<ActionState> {
  const interviewId = String(formData.get("interviewId") ?? "").trim();
  if (!interviewId) return { error: "Missing interview." };

  // Up to three competency rows: name_i / weight_i / score_i
  const competencies: CompetencyScore[] = [];
  for (let i = 0; i < 3; i++) {
    const name = String(formData.get(`name_${i}`) ?? "").trim();
    const weightRaw = String(formData.get(`weight_${i}`) ?? "").trim();
    const scoreRaw = String(formData.get(`score_${i}`) ?? "").trim();
    if (!name || !weightRaw || !scoreRaw) continue;
    const weight = Number(weightRaw);
    const score = Number(scoreRaw);
    if (Number.isNaN(weight) || weight <= 0) return { error: `Weight for "${name}" must be a positive number.` };
    if (Number.isNaN(score) || score < 1 || score > 5) return { error: `Score for "${name}" must be 1–5.` };
    competencies.push({ name, weight, score });
  }
  if (competencies.length === 0) return { error: "Add at least one competency (name, weight, score)." };

  const recommendation = String(formData.get("recommendation") ?? "").trim() || null;
  const notes = String(formData.get("notes") ?? "").trim() || null;

  const res = await submitScorecard(interviewId, { competencies, recommendation, notes });
  if (!res.ok) return { error: fail(res.status, "submit the scorecard") };
  revalidate();
  return { ok: true };
}
