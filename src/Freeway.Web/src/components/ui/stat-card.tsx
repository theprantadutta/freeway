import { LucideIcon } from "lucide-react";
import { ReactNode } from "react";
import { cn } from "@/lib/utils/cn";

/**
 * Stats read as one instrument panel divided by hairlines rather than a row of
 * separate floating cards, so the eye compares values instead of counting boxes.
 */
export function StatStrip({
  children,
  className,
  columns = 4,
}: {
  children: ReactNode;
  className?: string;
  columns?: 2 | 3 | 4;
}) {
  const cols = {
    2: "grid-cols-2",
    3: "grid-cols-2 sm:grid-cols-3",
    4: "grid-cols-2 lg:grid-cols-4",
  }[columns];

  return (
    <div
      className={cn(
        "grid overflow-hidden rounded-panel border border-line bg-panel",
        "divide-x divide-y divide-line [&>*:nth-child(-n+2)]:border-t-0",
        cols,
        className
      )}
    >
      {children}
    </div>
  );
}

interface StatProps {
  label: string;
  value: string | number;
  icon?: LucideIcon;
  /** Accent scope class, e.g. ACCENTS.premium.scope. */
  accent?: string;
  /** Small note under the value — units, period, or context. */
  note?: string;
  className?: string;
}

export function Stat({ label, value, icon: Icon, accent, note, className }: StatProps) {
  return (
    <div className={cn("relative p-4 sm:p-5", accent, className)}>
      <div className="flex items-center gap-2">
        {Icon && (
          <span className="grid h-6 w-6 shrink-0 place-items-center rounded-[0.3125rem] accent-tint accent-text">
            <Icon className="h-3.5 w-3.5" aria-hidden />
          </span>
        )}
        <p className="truncate text-sm text-muted">{label}</p>
      </div>
      <p className="metric mt-2 text-2xl font-semibold text-ink sm:text-3xl">{value}</p>
      {note && <p className="mt-1 text-xs text-subtle">{note}</p>}
    </div>
  );
}

interface StatCardProps {
  title: string;
  value: string | number;
  icon: LucideIcon;
  iconColor?: string;
  accent?: string;
  trend?: { value: number; isPositive: boolean };
  className?: string;
}

/** Standalone stat, for grids that are not a single strip. */
export function StatCard({
  title,
  value,
  icon: Icon,
  accent,
  trend,
  className,
}: StatCardProps) {
  return (
    <div
      className={cn(
        "rounded-panel border border-line bg-panel p-4",
        accent,
        className
      )}
    >
      <div className="flex items-center gap-2">
        <span className="grid h-6 w-6 shrink-0 place-items-center rounded-[0.3125rem] accent-tint accent-text">
          <Icon className="h-3.5 w-3.5" aria-hidden />
        </span>
        <p className="truncate text-sm text-muted">{title}</p>
      </div>
      <p className="metric mt-2 text-2xl font-semibold text-ink">{value}</p>
      {trend && (
        <p className={cn("mt-1 text-xs", trend.isPositive ? "text-ok" : "text-danger")}>
          {trend.isPositive ? "+" : ""}
          {trend.value}%
        </p>
      )}
    </div>
  );
}
