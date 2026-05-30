"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";

const STATUSES = ["", "active", "on_leave", "terminated"] as const;
const field = "rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900";

/// Search + status filter. Submitting navigates with new query params (resets the cursor,
/// so filtering always starts from the first page).
export function FilterBar({ search, status }: { search: string; status: string }) {
  const router = useRouter();
  const params = useSearchParams();
  const [value, setValue] = useState(search);

  function apply(next: { search?: string; status?: string }) {
    const qs = new URLSearchParams(params.toString());
    const s = next.search ?? value;
    const st = next.status ?? status;
    s ? qs.set("search", s) : qs.delete("search");
    st ? qs.set("status", st) : qs.delete("status");
    qs.delete("cursor"); // any filter change resets paging
    router.push(`/employees?${qs.toString()}`);
  }

  return (
    <div className="flex flex-wrap items-center gap-3">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          apply({});
        }}
        className="flex gap-2"
      >
        <input
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder="Search name, no, email…"
          className={`${field} w-64`}
        />
        <button className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800">
          Search
        </button>
      </form>

      <select value={status} onChange={(e) => apply({ status: e.target.value })} className={field} aria-label="Status filter">
        {STATUSES.map((s) => (
          <option key={s || "all"} value={s}>
            {s === "" ? "All statuses" : s}
          </option>
        ))}
      </select>

      {(search || status) && (
        <button
          onClick={() => {
            setValue("");
            router.push("/employees");
          }}
          className="text-sm text-gray-500 hover:text-gray-700 hover:underline"
        >
          Clear
        </button>
      )}
    </div>
  );
}
