import { Skeleton } from "@/components/ui";

/// Shared route-level loading skeleton for the authenticated app group.
export default function Loading() {
  return (
    <div className="mx-auto max-w-5xl px-6 py-8">
      <Skeleton className="h-7 w-48" />
      <Skeleton className="mt-3 h-4 w-72" />
      <div className="mt-8 space-y-2">
        <Skeleton className="h-10 w-full" />
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    </div>
  );
}
