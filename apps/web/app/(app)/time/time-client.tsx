"use client";

import { useActionState } from "react";
import type { AttendanceEntry } from "@/lib/api";
import {
  Alert,
  Badge,
  Button,
  Input,
  Label,
  Table,
  TableContainer,
  TBody,
  TD,
  TH,
  THead,
  TR,
} from "@/components/ui";
import { clockAction, type AttendanceState } from "./actions";

/// Deterministic, locale-independent timestamp render (UTC ISO -> "YYYY-MM-DD HH:mm") so server and
/// client markup match (no hydration mismatch).
function fmt(iso: string): string {
  return iso.replace("T", " ").slice(0, 16);
}

export function ClockForm() {
  const [state, action, pending] = useActionState<AttendanceState, FormData>(clockAction, {});
  return (
    <form
      action={action}
      className="grid grid-cols-1 gap-3 rounded-[var(--radius-app)] border border-border bg-surface p-5 shadow-sm sm:grid-cols-2 lg:grid-cols-3"
    >
      <Label className="lg:col-span-2">
        Employee ID
        <Input name="employeeId" placeholder="paste an employee id (from the Employees page)" required />
      </Label>
      <Label>
        Notes
        <Input name="notes" placeholder="(optional)" />
      </Label>
      <div className="flex items-end gap-2 lg:col-span-3">
        <Button type="submit" name="intent" value="in" disabled={pending}>
          Clock in
        </Button>
        <Button type="submit" name="intent" value="out" variant="secondary" disabled={pending}>
          Clock out
        </Button>
      </div>
      {state.error && (
        <Alert tone="danger" className="col-span-full">
          {state.error}
        </Alert>
      )}
      {state.ok && (
        <Alert tone="success" className="col-span-full">
          Done.
        </Alert>
      )}
    </form>
  );
}

export function AttendanceTable({ items }: { items: AttendanceEntry[] }) {
  return (
    <TableContainer>
      <Table>
        <THead>
          <TR>
            <TH>Date</TH>
            <TH>Clock in</TH>
            <TH>Clock out</TH>
            <TH>Hours</TH>
            <TH>Status</TH>
            <TH>Notes</TH>
          </TR>
        </THead>
        <TBody>
          {items.map((a) => (
            <TR key={a.id} data-testid="attendance-row" data-attendance-status={a.status}>
              <TD>{a.workDate}</TD>
              <TD className="text-muted-foreground">{fmt(a.clockIn)}</TD>
              <TD className="text-muted-foreground">{a.clockOut ? fmt(a.clockOut) : "—"}</TD>
              <TD>{a.hours != null ? a.hours.toFixed(2) : "—"}</TD>
              <TD>
                <Badge tone={a.status === "completed" ? "success" : "info"}>{a.status}</Badge>
              </TD>
              <TD className="text-muted-foreground">{a.notes ?? "—"}</TD>
            </TR>
          ))}
          {items.length === 0 && (
            <TR>
              <TD colSpan={6} className="py-10 text-center text-muted-foreground">
                No attendance yet.
              </TD>
            </TR>
          )}
        </TBody>
      </Table>
    </TableContainer>
  );
}
