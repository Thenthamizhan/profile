"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { createEmployee, updateEmployee, deleteEmployee } from "@/lib/api";
import { COOKIE } from "@/lib/session";

export type CreateState = { ok?: boolean; error?: string };
export type MutateState = { ok?: boolean; error?: string; deleted?: boolean };

function permError(status: number, verb: string): string {
  if (status === 403) return `You don't have permission to ${verb} employees.`;
  if (status === 404) return "Employee not found (it may have been removed).";
  return `${verb[0].toUpperCase()}${verb.slice(1)} failed (${status}).`;
}

export async function createEmployeeAction(_prev: CreateState, formData: FormData): Promise<CreateState> {
  const input = {
    companyId: String(formData.get("companyId") ?? "").trim(),
    employeeNo: String(formData.get("employeeNo") ?? "").trim(),
    firstName: String(formData.get("firstName") ?? "").trim(),
    lastName: String(formData.get("lastName") ?? "").trim(),
    workEmail: String(formData.get("workEmail") ?? "").trim() || null,
    hireDate: String(formData.get("hireDate") ?? "").trim() || null,
    nationalId: String(formData.get("nationalId") ?? "").trim() || null,
    dateOfBirth: String(formData.get("dateOfBirth") ?? "").trim() || null,
    bankAccount: String(formData.get("bankAccount") ?? "").trim() || null,
  };

  if (!input.employeeNo || !input.firstName || !input.lastName) {
    return { error: "Employee no, first name and last name are required." };
  }

  const res = await createEmployee(input);
  if (!res.ok) return { error: permError(res.status, "add") };

  revalidatePath("/employees");
  return { ok: true };
}

export async function updateEmployeeAction(_prev: MutateState, formData: FormData): Promise<MutateState> {
  const id = String(formData.get("id") ?? "").trim();
  if (!id) return { error: "Missing employee id." };

  const input = {
    firstName: String(formData.get("firstName") ?? "").trim() || null,
    lastName: String(formData.get("lastName") ?? "").trim() || null,
    workEmail: String(formData.get("workEmail") ?? "").trim() || null,
    status: String(formData.get("status") ?? "").trim() || null,
    nationalId: String(formData.get("nationalId") ?? "").trim() || null,
    dateOfBirth: String(formData.get("dateOfBirth") ?? "").trim() || null,
    bankAccount: String(formData.get("bankAccount") ?? "").trim() || null,
  };

  const res = await updateEmployee(id, input);
  if (!res.ok) return { error: permError(res.status, "update") };

  revalidatePath("/employees");
  return { ok: true };
}

export async function deleteEmployeeAction(_prev: MutateState, formData: FormData): Promise<MutateState> {
  const id = String(formData.get("id") ?? "").trim();
  if (!id) return { error: "Missing employee id." };

  const res = await deleteEmployee(id);
  if (!res.ok) return { error: permError(res.status, "delete") };

  revalidatePath("/employees");
  return { ok: true, deleted: true };
}

export async function logout(): Promise<void> {
  const store = await cookies();
  store.delete(COOKIE);
  redirect("/login");
}
