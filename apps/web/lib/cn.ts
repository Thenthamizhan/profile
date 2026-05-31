import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

/// Merge conditional class lists and de-conflict Tailwind utilities (last-wins).
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
