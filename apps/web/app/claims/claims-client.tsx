"use client";

import { useActionState } from "react";
import type { ClaimItem } from "@/lib/api";
import { submitClaimAction, decideClaimAction, type ClaimState } from "./actions";

const input = "rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900";
const btn = "rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50";

function StatusBadge({ status }: { status: string }) {
  const tone =
    status === "reimbursed" ? "bg-green-50 text-green-700"
    : status === "approved" ? "bg-blue-50 text-blue-700"
    : status === "rejected" ? "bg-red-50 text-red-600"
    : "bg-amber-50 text-amber-700"; // pending
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tone}`}>{status}</span>;
}

function money(amount: number, currency: string): string {
  // amount is exact (numeric on the server); format with thousands + 2dp.
  return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function SubmitClaimForm() {
  const [state, action, pending] = useActionState<ClaimState, FormData>(submitClaimAction, {});
  return (
    <form action={action} className="grid grid-cols-1 gap-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm sm:grid-cols-2 lg:grid-cols-3">
      <label className="flex flex-col gap-1 text-xs text-gray-600 lg:col-span-3">
        Employee ID
        <input name="employeeId" placeholder="paste an employee id (from the Employees page)" required className={input} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Category
        <select name="category" defaultValue="travel" className={input}>
          <option value="travel">travel</option>
          <option value="meals">meals</option>
          <option value="equipment">equipment</option>
          <option value="other">other</option>
        </select>
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Amount
        <input name="amount" type="number" min="0.01" step="0.01" placeholder="0.00" required className={input} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Currency
        <input name="currency" defaultValue="SGD" maxLength={3} className={input} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600 lg:col-span-2">
        Description
        <input name="description" placeholder="(optional)" className={input} />
      </label>
      <div className="flex items-end">
        <button type="submit" disabled={pending} className={btn}>{pending ? "Submitting…" : "Submit claim"}</button>
      </div>
      {state.error && <p className="col-span-full text-sm text-red-600">{state.error}</p>}
    </form>
  );
}

export function ClaimsTable({
  items,
  canApprove,
  canReimburse,
}: {
  items: ClaimItem[];
  canApprove: boolean;
  canReimburse: boolean;
}) {
  const showActions = canApprove || canReimburse;
  return (
    <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-gray-200 bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
          <tr>
            <th className="px-4 py-3">Category</th>
            <th className="px-4 py-3">Amount</th>
            <th className="px-4 py-3">Status</th>
            <th className="px-4 py-3">Description</th>
            {showActions && <th className="px-4 py-3 text-right">Actions</th>}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {items.map((c) => (
            <tr key={c.id} data-testid="claim-row" data-claim-status={c.status} className="text-gray-800">
              <td className="px-4 py-3">{c.category}</td>
              <td className="px-4 py-3 font-medium">{money(c.amount, c.currency)}</td>
              <td className="px-4 py-3"><StatusBadge status={c.status} /></td>
              <td className="px-4 py-3 text-gray-600">{c.description ?? "—"}</td>
              {showActions && (
                <td className="px-4 py-3 text-right">
                  {canApprove && c.status === "pending" && <DecideButtons id={c.id} />}
                  {canReimburse && c.status === "approved" && <ReimburseButton id={c.id} />}
                  {!(canApprove && c.status === "pending") && !(canReimburse && c.status === "approved") && (
                    <span className="text-xs text-gray-300">—</span>
                  )}
                </td>
              )}
            </tr>
          ))}
          {items.length === 0 && (
            <tr>
              <td colSpan={showActions ? 5 : 4} className="px-4 py-10 text-center text-sm text-gray-400">No claims.</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

function DecideButtons({ id }: { id: string }) {
  const [state, action, pending] = useActionState<ClaimState, FormData>(decideClaimAction, {});
  return (
    <form action={action} className="inline-flex gap-1" title={state.error ?? ""}>
      <input type="hidden" name="id" value={id} />
      <button type="submit" name="decision" value="approve" disabled={pending}
        className="rounded-md bg-blue-600 px-2 py-1 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50">Approve</button>
      <button type="submit" name="decision" value="reject" disabled={pending}
        className="rounded-md border border-red-200 px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-50">Reject</button>
    </form>
  );
}

function ReimburseButton({ id }: { id: string }) {
  const [state, action, pending] = useActionState<ClaimState, FormData>(decideClaimAction, {});
  return (
    <form action={action} className="inline-flex" title={state.error ?? ""}>
      <input type="hidden" name="id" value={id} />
      <button type="submit" name="decision" value="reimburse" disabled={pending}
        className="rounded-md bg-green-600 px-2 py-1 text-xs font-medium text-white hover:bg-green-700 disabled:opacity-50">
        {pending ? "…" : "Reimburse"}
      </button>
    </form>
  );
}
