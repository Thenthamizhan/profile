"use client";

import { useEffect, useState } from "react";
import { useActionState } from "react";
import type { Employee } from "@/lib/api";
import {
  updateEmployeeAction,
  deleteEmployeeAction,
  type MutateState,
} from "./actions";

const STATUSES = ["active", "on_leave", "terminated"] as const;

export function EmployeeTable({
  employees,
  canWrite,
  canDelete,
}: {
  employees: Employee[];
  canWrite: boolean;
  canDelete: boolean;
}) {
  const [selected, setSelected] = useState<Employee | null>(null);

  // After a revalidate, `employees` changes identity. Keep the open drawer in sync, and
  // close it if the selected employee no longer exists (e.g. it was deleted).
  useEffect(() => {
    if (!selected) return;
    const fresh = employees.find((e) => e.id === selected.id);
    if (!fresh) setSelected(null);
    else if (fresh !== selected) setSelected(fresh);
  }, [employees, selected]);

  return (
    <>
      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-gray-200 bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
            <tr>
              <th className="px-4 py-3">Employee no</th>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Work email</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Hire date</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {employees.map((e) => (
              <tr
                key={e.id}
                onClick={() => setSelected(e)}
                className="cursor-pointer text-gray-800 hover:bg-gray-50"
              >
                <td className="px-4 py-3 font-mono text-xs">{e.employeeNo}</td>
                <td className="px-4 py-3">
                  <button
                    className="font-medium text-gray-900 hover:underline"
                    onClick={(ev) => {
                      ev.stopPropagation();
                      setSelected(e);
                    }}
                  >
                    {e.firstName} {e.lastName}
                  </button>
                </td>
                <td className="px-4 py-3 text-gray-600">{e.workEmail ?? "—"}</td>
                <td className="px-4 py-3">
                  <StatusBadge status={e.status} />
                </td>
                <td className="px-4 py-3 text-gray-600">{e.hireDate ?? "—"}</td>
              </tr>
            ))}
            {employees.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-10 text-center text-sm text-gray-400">
                  No employees yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {selected && (
        <EmployeeDrawer
          employee={selected}
          canWrite={canWrite}
          canDelete={canDelete}
          onClose={() => setSelected(null)}
        />
      )}
    </>
  );
}

function StatusBadge({ status }: { status: string }) {
  const tone =
    status === "active"
      ? "bg-green-50 text-green-700"
      : status === "on_leave"
        ? "bg-amber-50 text-amber-700"
        : "bg-gray-100 text-gray-600";
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tone}`}>{status}</span>;
}

function EmployeeDrawer({
  employee,
  canWrite,
  canDelete,
  onClose,
}: {
  employee: Employee;
  canWrite: boolean;
  canDelete: boolean;
  onClose: () => void;
}) {
  // Close on Escape (a11y).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <button
        aria-label="Close panel"
        onClick={onClose}
        className="absolute inset-0 bg-black/30"
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-label={`Employee ${employee.employeeNo}`}
        className="relative flex h-full w-full max-w-md flex-col gap-5 overflow-y-auto bg-white p-6 shadow-xl"
      >
        <header className="flex items-start justify-between">
          <div>
            <h2 className="text-lg font-semibold text-gray-900">
              {employee.firstName} {employee.lastName}
            </h2>
            <p className="font-mono text-xs text-gray-500">{employee.employeeNo}</p>
          </div>
          <button
            onClick={onClose}
            className="rounded-md p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
            aria-label="Close"
          >
            ✕
          </button>
        </header>

        <dl className="grid grid-cols-3 gap-2 rounded-lg bg-gray-50 p-4 text-sm">
          <dt className="text-gray-500">Status</dt>
          <dd className="col-span-2"><StatusBadge status={employee.status} /></dd>
          <dt className="text-gray-500">Email</dt>
          <dd className="col-span-2 text-gray-800">{employee.workEmail ?? "—"}</dd>
          <dt className="text-gray-500">Hire date</dt>
          <dd className="col-span-2 text-gray-800">{employee.hireDate ?? "—"}</dd>
        </dl>

        {canWrite ? (
          <EditForm employee={employee} onDone={onClose} />
        ) : (
          <p className="text-sm text-gray-500">
            Read-only — you lack <code>employee.write</code>.
          </p>
        )}

        {canDelete && <DeleteControl employee={employee} onDone={onClose} />}
      </aside>
    </div>
  );
}

const field = "rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900";

function EditForm({ employee, onDone }: { employee: Employee; onDone: () => void }) {
  const [state, action, pending] = useActionState<MutateState, FormData>(updateEmployeeAction, {});

  useEffect(() => {
    if (state.ok) onDone();
  }, [state.ok, onDone]);

  return (
    <form action={action} className="flex flex-col gap-3 border-t border-gray-100 pt-5">
      <h3 className="text-sm font-medium text-gray-700">Edit</h3>
      <input type="hidden" name="id" value={employee.id} />
      <label className="flex flex-col gap-1 text-xs font-medium text-gray-600">
        First name
        <input name="firstName" defaultValue={employee.firstName} className={field} />
      </label>
      <label className="flex flex-col gap-1 text-xs font-medium text-gray-600">
        Last name
        <input name="lastName" defaultValue={employee.lastName} className={field} />
      </label>
      <label className="flex flex-col gap-1 text-xs font-medium text-gray-600">
        Work email
        <input name="workEmail" type="email" defaultValue={employee.workEmail ?? ""} className={field} />
      </label>
      <label className="flex flex-col gap-1 text-xs font-medium text-gray-600">
        Status
        <select name="status" defaultValue={employee.status} className={field}>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </label>
      <button
        type="submit"
        disabled={pending}
        className="mt-1 rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
      >
        {pending ? "Saving…" : "Save changes"}
      </button>
      {state.error && <p className="text-sm text-red-600">{state.error}</p>}
    </form>
  );
}

function DeleteControl({ employee, onDone }: { employee: Employee; onDone: () => void }) {
  const [state, action, pending] = useActionState<MutateState, FormData>(deleteEmployeeAction, {});
  const [confirming, setConfirming] = useState(false);

  useEffect(() => {
    if (state.deleted) onDone();
  }, [state.deleted, onDone]);

  if (!confirming) {
    return (
      <button
        onClick={() => setConfirming(true)}
        className="mt-auto rounded-md border border-red-200 px-4 py-2 text-sm font-medium text-red-600 hover:bg-red-50"
      >
        Delete employee
      </button>
    );
  }

  return (
    <form action={action} className="mt-auto flex flex-col gap-2 rounded-lg border border-red-200 bg-red-50 p-4">
      <p className="text-sm text-red-700">
        Delete <strong>{employee.firstName} {employee.lastName}</strong>? This soft-deletes the record.
      </p>
      <input type="hidden" name="id" value={employee.id} />
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={pending}
          className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
        >
          {pending ? "Deleting…" : "Confirm delete"}
        </button>
        <button
          type="button"
          onClick={() => setConfirming(false)}
          className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-white"
        >
          Cancel
        </button>
      </div>
      {state.error && <p className="text-sm text-red-700">{state.error}</p>}
    </form>
  );
}
