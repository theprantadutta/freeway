import { LucideIcon } from "lucide-react";
import { ReactNode } from "react";
import { cn } from "@/lib/utils/cn";

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description: string;
  action?: ReactNode;
  className?: string;
}

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center rounded-panel border border-dashed border-line-strong bg-panel/50 px-6 py-14 text-center",
        className
      )}
    >
      <div className="grid h-11 w-11 place-items-center rounded-panel accent-tint accent-text">
        <Icon className="h-5 w-5" aria-hidden />
      </div>
      <h3 className="mt-4 text-base font-semibold text-ink">{title}</h3>
      <p className="mt-1 max-w-sm text-sm text-muted">{description}</p>
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}

/** Shown when a query fails, with the retry in the interface's own voice. */
export function ErrorState({
  title = "Could not load this",
  description,
  onRetry,
  className,
}: {
  title?: string;
  description?: string;
  onRetry?: () => void;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "rounded-panel border border-danger/30 bg-danger/[0.06] px-5 py-4",
        className
      )}
      role="alert"
    >
      <p className="text-sm font-semibold text-danger">{title}</p>
      <p className="mt-0.5 text-sm text-muted">
        {description ?? "The gateway did not respond. Check that the API is reachable."}
      </p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="mt-3 text-sm font-medium text-danger underline underline-offset-4 hover:no-underline"
        >
          Try again
        </button>
      )}
    </div>
  );
}
