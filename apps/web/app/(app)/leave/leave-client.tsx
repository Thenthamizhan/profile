"use client";

import { useActionState } from "react";
import type { LeaveRequestItem } from "@/lib/api";
import { submitLeaveAction, decideLeaveAction, type LeaveState } from "./actions";

const input = "rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900";
const btn = "rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50";

function StatusBadge({ status }: { status: string }) {
  const tone =
    status === "approved" ? "bg-green-50 text-green-700"
    : status === "rejected" ? "bg-red-50 text-red-600"
    : status === "cancelled" ? "bg-gray-100 text-gray-500"
    : "bg-amber-50 text-amber-700"; // pending
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tone}`}>{status}</span>;
}

export function SubmitLeaveForm() {
  const [state, action, pending] = useActionState<LeaveState, FormData>(submitLeaveAction, {});
  return (
    <form action={action} className="grid grid-cols-1 gap-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm sm:grid-cols-2 lg:grid-cols-3">
      <label className="flex flex-col gap-1 text-xs text-gray-600 lg:col-span-3">
        Employee ID
        <input name="employeeId" placeholder="paste an employee id (from the Employees page)" required className={input} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Type
        <select name="leaveType" defaultValue="annual" className={input}>
          <option value="annual">annual</option>
          <option value="sick">sick</option>
          <option value="unpaid">unpaid</option>
        </select>
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Start date
        <input name="startDate" type="date" required className={input} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        End date
        <input name="endDate" type="date" required className={input} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Days
        <input name="days" type="number" min="0.5" step="0.5" defaultValue="1" required className={input} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600 lg:col-span-2">
        Reason
        <input name="reason" placeholder="(optional)" className={input} />
      </label>
      <div className="flex items-end">
        <button type="submit" disabled={pending} className={btn}>{pending ? "Submitting…" : "Submit leave"}</button>
      </div>
      {state.error && <p className="col-span-full text-sm text-red-600">{state.error}</p>}
    </form>
  );
}

export function LeaveTable({ items, canApprove }: { items: LeaveRequestItem[]; canApprove: boolean }) {
  return (
    <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-gray-200 bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
          <tr>
            <th className="px-4 py-3">Type</th>
            <th className="px-4 py-3">Dates</th>
            <th className="px-4 py-3">Days</th>
            <th className="px-4 py-3">Status</th>
            <th className="px-4 py-3">Reason</th>
            {canApprove && <th className="px-4 py-3 text-right">Decision</th>}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {items.map((l) => (
            <tr key={l.id} data-testid="leave-row" data-leave-status={l.status} className="text-gray-800">
              <td className="px-4 py-3">{l.leaveType}</td>
              <td className="px-4 py-3 text-gray-600">{l.startDate} → {l.endDate}</td>
              <td className="px-4 py-3">{l.days}</td>
              <td className="px-4 py-3"><StatusBadge status={l.status} /></td>
              <td className="px-4 py-3 text-gray-600">{l.reason ?? "—"}</td>
              {canApprove && (
                <td className="px-4 py-3 text-right">
                  {l.status === "pending" ? <DecideButtons id={l.id} /> : <span className="text-xs text-gray-300">—</span>}
                </td>
              )}
            </tr>
          ))}
          {items.length === 0 && (
            <tr>
              <td colSpan={canApprove ? 6 : 5} className="px-4 py-10 text-center text-sm text-gray-400">No leave requests.</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

function DecideButtons({ id }: { id: string }) {
  const [state, action, pending] = useActionState<LeaveState, FormData>(decideLeaveAction, {});
  return (
    <form action={action} className="inline-flex gap-1" title={state.error ?? ""}>
      <input type="hidden" name="id" value={id} />
      <button type="submit" name="decision" value="approve" disabled={pending}
        className="rounded-md bg-green-600 px-2 py-1 text-xs font-medium text-white hover:bg-green-700 disabled:opacity-50">Approve</button>
      <button type="submit" name="decision" value="reject" disabled={pending}
        className="rounded-md border border-red-200 px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-50">Reject</button>
    </form>
  );
}
