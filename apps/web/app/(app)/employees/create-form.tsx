"use client";

import { useActionState } from "react";
import { createEmployeeAction, type CreateState } from "./actions";

const input = "rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900";

export function CreateForm({ companyId }: { companyId: string }) {
  const [state, action, pending] = useActionState<CreateState, FormData>(createEmployeeAction, {});

  return (
    <form action={action} className="grid grid-cols-1 gap-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm sm:grid-cols-2 lg:grid-cols-3">
      <input name="employeeNo" placeholder="Employee no *" required className={input} />
      <input name="firstName" placeholder="First name *" required className={input} />
      <input name="lastName" placeholder="Last name *" required className={input} />
      <input name="workEmail" type="email" placeholder="Work email" className={input} />
      <input name="hireDate" type="date" aria-label="Hire date" className={input} />
      <input type="hidden" name="companyId" value={companyId} />
      <button
        type="submit"
        disabled={pending}
        className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
      >
        {pending ? "Saving…" : "Add employee"}
      </button>
      {state.error && <p className="col-span-full text-sm text-red-600">{state.error}</p>}
      {state.ok && <p className="col-span-full text-sm text-green-600">Employee added.</p>}
    </form>
  );
}
