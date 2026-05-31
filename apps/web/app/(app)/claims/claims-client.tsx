"use client";

import { useActionState } from "react";
import type { ClaimItem } from "@/lib/api";
import {
  Alert,
  Badge,
  type BadgeProps,
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
import { submitClaimAction, decideClaimAction, type ClaimState } from "./actions";

const selectClass =
  "h-9 w-full rounded-[var(--radius-app)] border border-input bg-surface px-3 text-sm text-foreground shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

/// Claim-specific tone: `approved` (awaiting reimbursement) reads as info, distinct from the
/// terminal `reimbursed` success state.
function claimTone(status: string): NonNullable<BadgeProps["tone"]> {
  switch (status) {
    case "reimbursed":
      return "success";
    case "approved":
      return "info";
    case "rejected":
      return "danger";
    default:
      return "warning"; // pending
  }
}

function money(amount: number, currency: string): string {
  // amount is exact (numeric on the server); format with thousands + 2dp.
  return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function SubmitClaimForm() {
  const [state, action, pending] = useActionState<ClaimState, FormData>(submitClaimAction, {});
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
        Category
        <select name="category" defaultValue="travel" className={selectClass}>
          <option value="travel">travel</option>
          <option value="meals">meals</option>
          <option value="equipment">equipment</option>
          <option value="other">other</option>
        </select>
      </Label>
      <Label>
        Amount
        <Input name="amount" type="number" min="0.01" step="0.01" placeholder="0.00" required />
      </Label>
      <Label>
        Currency
        <Input name="currency" defaultValue="SGD" maxLength={3} />
      </Label>
      <Label className="lg:col-span-2">
        Description
        <Input name="description" placeholder="(optional)" />
      </Label>
      <div className="flex items-end">
        <Button type="submit" disabled={pending}>
          {pending ? "Submitting…" : "Submit claim"}
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
    <TableContainer>
      <Table>
        <THead>
          <TR>
            <TH>Category</TH>
            <TH>Amount</TH>
            <TH>Status</TH>
            <TH>Description</TH>
            {showActions && <TH className="text-right">Actions</TH>}
          </TR>
        </THead>
        <TBody>
          {items.map((c) => (
            <TR key={c.id} data-testid="claim-row" data-claim-status={c.status}>
              <TD>{c.category}</TD>
              <TD className="font-medium">{money(c.amount, c.currency)}</TD>
              <TD>
                <Badge tone={claimTone(c.status)}>{c.status}</Badge>
              </TD>
              <TD className="text-muted-foreground">{c.description ?? "—"}</TD>
              {showActions && (
                <TD className="text-right">
                  {canApprove && c.status === "pending" && <DecideButtons id={c.id} />}
                  {canReimburse && c.status === "approved" && <ReimburseButton id={c.id} />}
                  {!(canApprove && c.status === "pending") && !(canReimburse && c.status === "approved") && (
                    <span className="text-xs text-muted-foreground">—</span>
                  )}
                </TD>
              )}
            </TR>
          ))}
          {items.length === 0 && (
            <TR>
              <TD colSpan={showActions ? 5 : 4} className="py-10 text-center text-muted-foreground">
                No claims.
              </TD>
            </TR>
          )}
        </TBody>
      </Table>
    </TableContainer>
  );
}

function DecideButtons({ id }: { id: string }) {
  const [state, action, pending] = useActionState<ClaimState, FormData>(decideClaimAction, {});
  return (
    <form action={action} className="inline-flex gap-1" title={state.error ?? ""}>
      <input type="hidden" name="id" value={id} />
      <Button type="submit" name="decision" value="approve" size="sm" disabled={pending}>
        Approve
      </Button>
      <Button type="submit" name="decision" value="reject" variant="danger" size="sm" disabled={pending}>
        Reject
      </Button>
    </form>
  );
}

function ReimburseButton({ id }: { id: string }) {
  const [state, action, pending] = useActionState<ClaimState, FormData>(decideClaimAction, {});
  return (
    <form action={action} className="inline-flex" title={state.error ?? ""}>
      <input type="hidden" name="id" value={id} />
      <Button type="submit" name="decision" value="reimburse" variant="success" size="sm" disabled={pending}>
        {pending ? "…" : "Reimburse"}
      </Button>
    </form>
  );
}
