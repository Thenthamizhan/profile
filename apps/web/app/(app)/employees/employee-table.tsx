"use client";

import { useEffect, useState } from "react";
import { useActionState } from "react";
import { X } from "lucide-react";
import type { Employee } from "@/lib/api";
import {
  Alert,
  Badge,
  Button,
  Input,
  Label,
  statusTone,
  Table,
  TableContainer,
  TBody,
  TD,
  TH,
  THead,
  TR,
} from "@/components/ui";
import {
  updateEmployeeAction,
  deleteEmployeeAction,
  type MutateState,
} from "./actions";

const STATUSES = ["active", "on_leave", "terminated"] as const;
const selectClass =
  "h-9 w-full rounded-[var(--radius-app)] border border-input bg-surface px-3 text-sm text-foreground shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

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
      <TableContainer>
        <Table>
          <THead>
            <TR>
              <TH>Employee no</TH>
              <TH>Name</TH>
              <TH>Work email</TH>
              <TH>Status</TH>
              <TH>Hire date</TH>
            </TR>
          </THead>
          <TBody>
            {employees.map((e) => (
              <TR
                key={e.id}
                onClick={() => setSelected(e)}
                className="cursor-pointer transition-colors hover:bg-surface-muted"
              >
                <TD className="font-mono text-xs">{e.employeeNo}</TD>
                <TD>
                  <button
                    className="font-medium text-foreground hover:underline"
                    onClick={(ev) => {
                      ev.stopPropagation();
                      setSelected(e);
                    }}
                  >
                    {e.firstName} {e.lastName}
                  </button>
                </TD>
                <TD className="text-muted-foreground">{e.workEmail ?? "—"}</TD>
                <TD>
                  <Badge tone={statusTone(e.status)}>{e.status}</Badge>
                </TD>
                <TD className="text-muted-foreground">{e.hireDate ?? "—"}</TD>
              </TR>
            ))}
            {employees.length === 0 && (
              <TR>
                <TD colSpan={5} className="py-10 text-center text-muted-foreground">
                  No employees yet.
                </TD>
              </TR>
            )}
          </TBody>
        </Table>
      </TableContainer>

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
      <button aria-label="Close panel" onClick={onClose} className="absolute inset-0 bg-foreground/30" />
      <aside
        role="dialog"
        aria-modal="true"
        aria-label={`Employee ${employee.employeeNo}`}
        className="relative flex h-full w-full max-w-md flex-col gap-5 overflow-y-auto bg-surface p-6 shadow-xl"
      >
        <header className="flex items-start justify-between">
          <div>
            <h2 className="text-lg font-semibold text-foreground">
              {employee.firstName} {employee.lastName}
            </h2>
            <p className="font-mono text-xs text-muted-foreground">{employee.employeeNo}</p>
          </div>
          <Button variant="ghost" size="icon" onClick={onClose} aria-label="Close">
            <X />
          </Button>
        </header>

        <dl className="grid grid-cols-3 gap-2 rounded-[var(--radius-app)] bg-surface-muted p-4 text-sm">
          <dt className="text-muted-foreground">Status</dt>
          <dd className="col-span-2">
            <Badge tone={statusTone(employee.status)}>{employee.status}</Badge>
          </dd>
          <dt className="text-muted-foreground">Email</dt>
          <dd className="col-span-2 text-foreground">{employee.workEmail ?? "—"}</dd>
          <dt className="text-muted-foreground">Hire date</dt>
          <dd className="col-span-2 text-foreground">{employee.hireDate ?? "—"}</dd>
        </dl>

        {canWrite ? (
          <EditForm employee={employee} onDone={onClose} />
        ) : (
          <p className="text-sm text-muted-foreground">
            Read-only — you lack <code>employee.write</code>.
          </p>
        )}

        {canDelete && <DeleteControl employee={employee} onDone={onClose} />}
      </aside>
    </div>
  );
}

function EditForm({ employee, onDone }: { employee: Employee; onDone: () => void }) {
  const [state, action, pending] = useActionState<MutateState, FormData>(updateEmployeeAction, {});

  useEffect(() => {
    if (state.ok) onDone();
  }, [state.ok, onDone]);

  return (
    <form action={action} className="flex flex-col gap-3 border-t border-border pt-5">
      <h3 className="text-sm font-medium text-foreground">Edit</h3>
      <input type="hidden" name="id" value={employee.id} />
      <Label>
        First name
        <Input name="firstName" defaultValue={employee.firstName} />
      </Label>
      <Label>
        Last name
        <Input name="lastName" defaultValue={employee.lastName} />
      </Label>
      <Label>
        Work email
        <Input name="workEmail" type="email" defaultValue={employee.workEmail ?? ""} />
      </Label>
      <Label>
        Status
        <select name="status" defaultValue={employee.status} className={selectClass}>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </Label>
      <Button type="submit" disabled={pending} className="mt-1">
        {pending ? "Saving…" : "Save changes"}
      </Button>
      {state.error && <Alert tone="danger">{state.error}</Alert>}
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
      <Button variant="danger" onClick={() => setConfirming(true)} className="mt-auto">
        Delete employee
      </Button>
    );
  }

  return (
    <form
      action={action}
      className="mt-auto flex flex-col gap-2 rounded-[var(--radius-app)] border border-danger/30 bg-danger-bg p-4"
    >
      <p className="text-sm text-danger">
        Delete{" "}
        <strong>
          {employee.firstName} {employee.lastName}
        </strong>
        ? This soft-deletes the record.
      </p>
      <input type="hidden" name="id" value={employee.id} />
      <div className="flex gap-2">
        <Button type="submit" variant="success" size="sm" disabled={pending} className="bg-danger hover:bg-danger/90">
          {pending ? "Deleting…" : "Confirm delete"}
        </Button>
        <Button type="button" variant="secondary" size="sm" onClick={() => setConfirming(false)}>
          Cancel
        </Button>
      </div>
      {state.error && <p className="text-sm text-danger">{state.error}</p>}
    </form>
  );
}
