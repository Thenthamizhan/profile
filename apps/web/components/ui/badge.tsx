import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/cn";

const badgeVariants = cva(
  "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium",
  {
    variants: {
      tone: {
        neutral: "bg-surface-muted text-muted-foreground",
        success: "bg-success-bg text-success",
        warning: "bg-warning-bg text-warning",
        danger: "bg-danger-bg text-danger",
        info: "bg-info-bg text-info",
        brand: "bg-accent text-accent-foreground",
      },
    },
    defaultVariants: { tone: "neutral" },
  },
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

export function Badge({ className, tone, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ tone }), className)} {...props} />;
}

/// Map a domain status string to a badge tone, shared by every status column in the app.
export function statusTone(status: string): NonNullable<BadgeProps["tone"]> {
  switch (status) {
    case "active":
    case "approved":
    case "hired":
    case "accepted":
    case "reimbursed":
      return "success";
    case "on_leave":
    case "pending":
    case "sent":
    case "draft":
      return "warning";
    case "rejected":
    case "declined":
    case "terminated":
      return "danger";
    default:
      return "neutral";
  }
}
