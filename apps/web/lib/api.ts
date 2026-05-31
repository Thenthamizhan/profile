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

export type Page<T> = { items: T[]; nextCursor: string | null };

export type EmployeeQuery = {
  search?: string;
  status?: string;
  cursor?: string;
  limit?: number;
};

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

export async function listEmployees(query: EmployeeQuery = {}): Promise<Page<Employee>> {
  const qs = new URLSearchParams();
  if (query.search) qs.set("search", query.search);
  if (query.status) qs.set("status", query.status);
  if (query.cursor) qs.set("cursor", query.cursor);
  if (query.limit) qs.set("limit", String(query.limit));
  const suffix = qs.toString() ? `?${qs}` : "";

  const res = await authed(`/v1/employees${suffix}`);
  if (!res.ok) throw new Error(`Failed to load employees (${res.status})`);
  return (await res.json()) as Page<Employee>;
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

// ---- Recruitment / ATS ----

export type Job = { id: string; title: string; status: string; location: string | null; employmentType: string | null; pipelineId: string };
export type KanbanCard = { applicationId: string; candidateId: string; candidateName: string; matchScore: number | null; stage: string };
export type StageColumn = { key: string; name: string; cards: KanbanCard[] };
export type Board = { jobId: string; jobTitle: string; columns: StageColumn[] };

export async function listJobs(): Promise<Job[]> {
  const res = await authed("/v1/jobs");
  if (!res.ok) throw new Error(`Failed to load jobs (${res.status})`);
  return (await res.json()) as Job[];
}

export async function getBoard(jobId: string): Promise<Board | null> {
  const res = await authed(`/v1/jobs/${jobId}/board`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Failed to load board (${res.status})`);
  return (await res.json()) as Board;
}

export async function moveApplication(id: string, toStage: string): Promise<Result> {
  const res = await authed(`/v1/applications/${id}/move`, { method: "POST", body: JSON.stringify({ toStage }) });
  return { ok: res.ok, status: res.status };
}

// ---- Offers ----

export type Offer = {
  id: string;
  applicationId: string;
  salary: number | null;
  currency: string | null;
  status: string; // draft|sent|accepted|declined
  sentAt: string | null;
  respondedAt: string | null;
};

export async function listOffers(applicationId: string): Promise<Offer[]> {
  const res = await authed(`/v1/applications/${applicationId}/offers`);
  if (!res.ok) throw new Error(`Failed to load offers (${res.status})`);
  return (await res.json()) as Offer[];
}

export async function createOffer(
  applicationId: string,
  input: { salary: number | null; currency: string | null },
): Promise<Result> {
  const res = await authed(`/v1/applications/${applicationId}/offers`, { method: "POST", body: JSON.stringify(input) });
  return { ok: res.ok, status: res.status };
}

export async function sendOffer(offerId: string): Promise<Result> {
  const res = await authed(`/v1/offers/${offerId}/send`, { method: "POST" });
  return { ok: res.ok, status: res.status };
}

export async function respondOffer(offerId: string, decision: "accepted" | "declined"): Promise<Result> {
  const res = await authed(`/v1/offers/${offerId}/respond`, { method: "POST", body: JSON.stringify({ decision }) });
  return { ok: res.ok, status: res.status };
}

// ---- Interviews / scorecards ----

export type Interview = {
  id: string;
  applicationId: string;
  scheduledAt: string | null;
  interviewers: string[];
  rollupScore: number | null;
  recommendation: string | null;
};

export type CompetencyScore = { name: string; weight: number; score: number };

export async function listInterviews(applicationId: string): Promise<Interview[]> {
  const res = await authed(`/v1/applications/${applicationId}/interviews`);
  if (!res.ok) throw new Error(`Failed to load interviews (${res.status})`);
  return (await res.json()) as Interview[];
}

export async function scheduleInterview(
  applicationId: string,
  input: { scheduledAt: string | null; interviewers: string[] },
): Promise<Result> {
  const res = await authed(`/v1/applications/${applicationId}/interviews`, { method: "POST", body: JSON.stringify(input) });
  return { ok: res.ok, status: res.status };
}

export async function submitScorecard(
  interviewId: string,
  input: { competencies: CompetencyScore[]; recommendation: string | null; notes: string | null },
): Promise<Result> {
  const res = await authed(`/v1/interviews/${interviewId}/scorecard`, { method: "POST", body: JSON.stringify(input) });
  return { ok: res.ok, status: res.status };
}

// ---- Leave & Claims ----

export type LeaveRequestItem = {
  id: string;
  employeeId: string;
  leaveType: string;
  startDate: string;
  endDate: string;
  days: number;
  status: string; // pending|approved|rejected|cancelled
  reason: string | null;
};

export async function listLeave(status?: string): Promise<LeaveRequestItem[]> {
  const suffix = status ? `?status=${encodeURIComponent(status)}` : "";
  const res = await authed(`/v1/leave-requests${suffix}`);
  if (!res.ok) throw new Error(`Failed to load leave requests (${res.status})`);
  return (await res.json()) as LeaveRequestItem[];
}

export async function submitLeave(input: {
  employeeId: string;
  leaveType: string;
  startDate: string;
  endDate: string;
  days: number;
  reason: string | null;
}): Promise<Result> {
  const res = await authed("/v1/leave-requests", { method: "POST", body: JSON.stringify(input) });
  return { ok: res.ok, status: res.status };
}

export async function decideLeave(id: string, decision: "approve" | "reject"): Promise<Result> {
  const res = await authed(`/v1/leave-requests/${id}/${decision}`, { method: "POST" });
  return { ok: res.ok, status: res.status };
}

// ---- Claims (expense claims) ----

export type ClaimItem = {
  id: string;
  employeeId: string;
  category: string;
  amount: number;
  currency: string;
  status: string; // pending|approved|rejected|reimbursed
  description: string | null;
};

export async function listClaims(status?: string): Promise<ClaimItem[]> {
  const suffix = status ? `?status=${encodeURIComponent(status)}` : "";
  const res = await authed(`/v1/claims${suffix}`);
  if (!res.ok) throw new Error(`Failed to load claims (${res.status})`);
  return (await res.json()) as ClaimItem[];
}

export async function submitClaim(input: {
  employeeId: string;
  category: string;
  amount: number;
  currency: string | null;
  description: string | null;
}): Promise<Result> {
  const res = await authed("/v1/claims", { method: "POST", body: JSON.stringify(input) });
  return { ok: res.ok, status: res.status };
}

/// decision is one of the claim transitions: approve | reject | reimburse.
export async function decideClaim(id: string, decision: "approve" | "reject" | "reimburse"): Promise<Result> {
  const res = await authed(`/v1/claims/${id}/${decision}`, { method: "POST" });
  return { ok: res.ok, status: res.status };
}
