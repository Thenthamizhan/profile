"use client";

import { useActionState } from "react";
import { createEmployeeAction, type CreateState } from "./actions";
import { Alert, Button, Input } from "@/components/ui";

export function CreateForm({ companyId }: { companyId: string }) {
  const [state, action, pending] = useActionState<CreateState, FormData>(createEmployeeAction, {});

  return (
    <form
      action={action}
      className="grid grid-cols-1 gap-3 rounded-[var(--radius-app)] border border-border bg-surface p-5 shadow-sm sm:grid-cols-2 lg:grid-cols-3"
    >
      <Input name="employeeNo" placeholder="Employee no *" required />
      <Input name="firstName" placeholder="First name *" required />
      <Input name="lastName" placeholder="Last name *" required />
      <Input name="workEmail" type="email" placeholder="Work email" />
      <Input name="hireDate" type="date" aria-label="Hire date" />
      <Input name="nationalId" placeholder="National ID (NRIC/FIN)" aria-label="National ID" autoComplete="off" />
      <Input name="dateOfBirth" type="date" aria-label="Date of birth" />
      <Input name="bankAccount" placeholder="Bank account" aria-label="Bank account" autoComplete="off" />
      <input type="hidden" name="companyId" value={companyId} />
      <p className="col-span-full -mt-1 text-xs text-muted-foreground">
        National ID, date of birth and bank account are encrypted at rest.
      </p>
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Add employee"}
      </Button>
      {state.error && (
        <Alert tone="danger" className="col-span-full">
          {state.error}
        </Alert>
      )}
      {state.ok && (
        <Alert tone="success" className="col-span-full">
          Employee added.
        </Alert>
      )}
    </form>
  );
}
