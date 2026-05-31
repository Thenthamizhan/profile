"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { mintDevToken } from "@/lib/api";
import { COOKIE } from "@/lib/session";

export async function login(formData: FormData): Promise<void> {
  const tenantId = String(formData.get("tenantId") ?? "").trim();
  const userId = String(formData.get("userId") ?? "").trim();

  const token = await mintDevToken(tenantId, userId);

  const store = await cookies();
  store.set(COOKIE, token, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production", // HTTPS-only off localhost
    path: "/",
    maxAge: 60 * 60,
  });

  redirect("/employees");
}
