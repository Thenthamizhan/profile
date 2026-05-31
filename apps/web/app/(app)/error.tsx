"use client";

import { TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui";

/// Route-level error boundary for the authenticated app group. Client component per Next.js
/// contract; `reset()` re-renders the segment.
export default function Error({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <div className="mx-auto flex max-w-md flex-col items-center px-6 py-20 text-center">
      <div className="flex size-12 items-center justify-center rounded-full bg-danger-bg text-danger">
        <TriangleAlert className="size-6" aria-hidden />
      </div>
      <h2 className="mt-4 text-lg font-semibold text-foreground">Something went wrong</h2>
      <p className="mt-2 text-sm text-muted-foreground">
        {error.message || "An unexpected error occurred while loading this page."}
      </p>
      <Button className="mt-6" onClick={reset}>
        Try again
      </Button>
    </div>
  );
}
