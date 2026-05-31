"use client";

import { useActionState } from "react";
import type { LeaveRequestItem } from "@/lib/api";
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
import { submitLeaveAction, decideLeaveAction, type LeaveState } from "./actions";

const selectClass =
  "h-9 w-full rounded-[var(--radius-app)] border border-input bg-surface px-3 text-sm text-foreground shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

export function SubmitLeaveForm() {
  const [state, action, pending] = useActionState<LeaveState, FormData>(submitLeaveAction, {});
  return (
    <form
      action={action}
      className="grid grid-cols-1 gap-3 rounded-[var(--radius-app)] border border-border bg-surface p-5 shadow-sm sm:grid-cols-2 lg:grid-cols-3"
    >
      <Label className="lg:col-span-3">
        Employee ID
        <Input name="employeeId" placeholder="paste an employee id (from the Employees page)" required />
      </Label>
      <Label>
        Type
        <select name="leaveType" defaultValue="annual" className={selectClass}>
          <option value="annual">annual</option>
          <option value="sick">sick</option>
          <option value="unpaid">unpaid</option>
        </select>
      </Label>
      <Label>
        Start date
        <Input name="startDate" type="date" required />
      </Label>
      <Label>
        End date
        <Input name="endDate" type="date" required />
      </Label>
      <Label>
        Days
        <Input name="days" type="number" min="0.5" step="0.5" defaultValue="1" required />
      </Label>
      <Label className="lg:col-span-2">
        Reason
        <Input name="reason" placeholder="(optional)" />
      </Label>
      <div className="flex items-end">
        <Button type="submit" disabled={pending}>
          {pending ? "Submitting…" : "Submit leave"}
        </Button>
      </div>
      {state.error && (
        <Alert tone="danger" className="col-span-full">
          {state.error}
        </Alert>
      )}
    </form>
  );
}

export function LeaveTable({ items, canApprove }: { items: LeaveRequestItem[]; canApprove: boolean }) {
  return (
    <TableContainer>
      <Table>
        <THead>
          <TR>
            <TH>Type</TH>
            <TH>Dates</TH>
            <TH>Days</TH>
            <TH>Status</TH>
            <TH>Reason</TH>
            {canApprove && <TH className="text-right">Decision</TH>}
          </TR>
        </THead>
        <TBody>
          {items.map((l) => (
            <TR key={l.id} data-testid="leave-row" data-leave-status={l.status}>
              <TD>{l.leaveType}</TD>
              <TD className="text-muted-foreground">
                {l.startDate} → {l.endDate}
              </TD>
              <TD>{l.days}</TD>
              <TD>
                <Badge tone={statusTone(l.status)}>{l.status}</Badge>
              </TD>
              <TD className="text-muted-foreground">{l.reason ?? "—"}</TD>
              {canApprove && (
                <TD className="text-right">
                  {l.status === "pending" ? (
                    <DecideButtons id={l.id} />
                  ) : (
                    <span className="text-xs text-muted-foreground">—</span>
                  )}
                </TD>
              )}
            </TR>
          ))}
          {items.length === 0 && (
            <TR>
              <TD colSpan={canApprove ? 6 : 5} className="py-10 text-center text-muted-foreground">
                No leave requests.
              </TD>
            </TR>
          )}
        </TBody>
      </Table>
    </TableContainer>
  );
}

function DecideButtons({ id }: { id: string }) {
  const [state, action, pending] = useActionState<LeaveState, FormData>(decideLeaveAction, {});
  return (
    <form action={action} className="inline-flex gap-1" title={state.error ?? ""}>
      <input type="hidden" name="id" value={id} />
      <Button type="submit" name="decision" value="approve" variant="success" size="sm" disabled={pending}>
        Approve
      </Button>
      <Button type="submit" name="decision" value="reject" variant="danger" size="sm" disabled={pending}>
        Reject
      </Button>
    </form>
  );
}
