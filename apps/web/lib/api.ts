import { getToken } from "./session";

// Server-side only. The browser never sees the API or the token (no CORS, token stays in an
// httpOnly cookie). This Next app acts as a thin BFF in front of the ASP.NET Core API.
const API_URL = process.env.SAHAHR_API_URL ?? "http://127.0.0.1:5080";

export type Employee = {
  id: string;
  companyId: string;
  employeeNo: string;
  firstName: string;
  lastName: string;
  workEmail: string | null;
  status: string;
  hireDate: string | null;
};

export type CreateInput = {
  companyId: string;
  employeeNo: string;
  firstName: string;
  lastName: string;
  workEmail: string | null;
  hireDate: string | null;
};

// Matches UpdateEmployeeRequest on the API — all fields optional (partial update).
export type UpdateInput = {
  firstName?: string | null;
  lastName?: string | null;
  workEmail?: string | null;
  status?: string | null;
};

export type Result = { ok: boolean; status: number };

export async function mintDevToken(tenantId: string, userId: string): Promise<string> {
  const res = await fetch(`${API_URL}/v1/dev/token`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ tenantId, userId }),
    cache: "no-store",
  });
  if (!res.ok) throw new Error(`Dev token mint failed (${res.status})`);
  const data = (await res.json()) as { accessToken: string };
  return data.accessToken;
}

async function authed(path: string, init?: RequestInit): Promise<Response> {
  const token = await getToken();
  return fetch(`${API_URL}${path}`, {
    ...init,
    headers: {
      "content-type": "application/json",
      ...(init?.headers ?? {}),
      ...(token ? { authorization: `Bearer ${token}` } : {}),
    },
    cache: "no-store",
  });
}

export async function listEmployees(): Promise<Employee[]> {
  const res = await authed("/v1/employees");
  if (!res.ok) throw new Error(`Failed to load employees (${res.status})`);
  return (await res.json()) as Employee[];
}

export async function createEmployee(input: CreateInput): Promise<Result> {
  const res = await authed("/v1/employees", { method: "POST", body: JSON.stringify(input) });
  return { ok: res.ok, status: res.status };
}

export async function getEmployee(id: string): Promise<Employee | null> {
  const res = await authed(`/v1/employees/${id}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Failed to load employee (${res.status})`);
  return (await res.json()) as Employee;
}

export async function updateEmployee(id: string, input: UpdateInput): Promise<Result> {
  const res = await authed(`/v1/employees/${id}`, { method: "PUT", body: JSON.stringify(input) });
  return { ok: res.ok, status: res.status };
}

export async function deleteEmployee(id: string): Promise<Result> {
  const res = await authed(`/v1/employees/${id}`, { method: "DELETE" });
  return { ok: res.ok, status: res.status };
}
