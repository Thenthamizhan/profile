"use server";

import { revalidatePath } from "next/cache";
import { moveApplication } from "@/lib/api";

export type MoveState = { ok?: boolean; error?: string };

export async function moveApplicationAction(_prev: MoveState, formData: FormData): Promise<MoveState> {
  const id = String(formData.get("applicationId") ?? "").trim();
  const toStage = String(formData.get("toStage") ?? "").trim();
  if (!id || !toStage) return { error: "Missing application or target stage." };

  const res = await moveApplication(id, toStage);
  if (!res.ok) {
    return { error: res.status === 403 ? "You don't have permission to move candidates." : `Move failed (${res.status}).` };
  }

  revalidatePath("/recruitment/[jobId]", "page");
  return { ok: true };
}
