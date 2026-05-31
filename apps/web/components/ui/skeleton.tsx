import { cn } from "@/lib/cn";

/// Pulsing placeholder for loading.tsx route skeletons. Set width/height via className.
export function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("animate-pulse rounded-[var(--radius-app)] bg-surface-muted", className)} {...props} />;
}
