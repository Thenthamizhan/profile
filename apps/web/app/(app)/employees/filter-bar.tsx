"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Button, Input } from "@/components/ui";

const STATUSES = ["", "active", "on_leave", "terminated"] as const;

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
        <Input
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder="Search name, no, email…"
          className="w-64"
        />
        <Button type="submit" variant="secondary">
          Search
        </Button>
      </form>

      {/* Native select keeps this control simple and form-friendly; the Radix Select is reserved
          for richer cases. Styled to match the Input. */}
      <select
        value={status}
        onChange={(e) => apply({ status: e.target.value })}
        aria-label="Status filter"
        className="h-9 rounded-[var(--radius-app)] border border-input bg-surface px-3 text-sm text-foreground shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
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
          className="text-sm text-muted-foreground hover:text-foreground hover:underline"
        >
          Clear
        </button>
      )}
    </div>
  );
}
