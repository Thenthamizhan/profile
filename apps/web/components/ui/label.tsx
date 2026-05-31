import * as React from "react";
import { cn } from "@/lib/cn";

/// Small uppercase field label used across forms. Wrap an input as a child for an accessible pair.
export const Label = React.forwardRef<HTMLLabelElement, React.LabelHTMLAttributes<HTMLLabelElement>>(
  ({ className, ...props }, ref) => (
    <label
      ref={ref}
      className={cn("flex flex-col gap-1.5 text-xs font-medium text-muted-foreground", className)}
      {...props}
    />
  ),
);
Label.displayName = "Label";
