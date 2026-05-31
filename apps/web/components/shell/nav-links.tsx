"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Users, Briefcase, CalendarDays, Receipt, Clock, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/cn";

export type NavItem = { href: string; label: string; icon: keyof typeof ICONS };

const ICONS = {
  employees: Users,
  recruitment: Briefcase,
  leave: CalendarDays,
  claims: Receipt,
  time: Clock,
} satisfies Record<string, LucideIcon>;

/// Permission-filtered nav, shared by the desktop sidebar (vertical) and the mobile strip
/// (horizontal). Active state is derived from the URL so it stays correct across the persistent
/// layout without per-page `active` props.
export function NavLinks({
  items,
  orientation = "vertical",
}: {
  items: NavItem[];
  orientation?: "vertical" | "horizontal";
}) {
  const pathname = usePathname();
  return (
    <nav
      aria-label="Primary"
      className={cn(
        "gap-1",
        orientation === "vertical" ? "flex flex-col px-3" : "flex overflow-x-auto px-4 py-2",
      )}
    >
      {items.map((item) => {
        const Icon = ICONS[item.icon];
        const active = pathname === item.href || pathname.startsWith(item.href + "/");
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "flex shrink-0 items-center gap-3 rounded-[var(--radius-app)] px-3 py-2 text-sm font-medium transition-colors",
              active
                ? "bg-accent text-accent-foreground"
                : "text-muted-foreground hover:bg-surface-muted hover:text-foreground",
            )}
          >
            <Icon className="size-4 shrink-0" aria-hidden />
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
