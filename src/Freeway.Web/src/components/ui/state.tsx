import { LucideIcon } from "lucide-react";
import { ReactNode } from "react";
import { cn } from "@/lib/utils/cn";

export function Empty({
  icon: Icon,
  title,
  body,
  action,
  className,
}: {
  icon: LucideIcon;
  title: string;
  body: string;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center px-6 py-16 text-center",
        className
      )}
    >
      <span className="grid h-10 w-10 place-items-center rounded lane-soft lane-fg">
        <Icon className="h-4 w-4" aria-hidden />
      </span>
      <h3 className="mt-4 text-base font-semibold text-text">{title}</h3>
      <p className="mt-1 max-w-sm text-sm text-text-2">{body}</p>
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}

/** Shown when a query fails, in the interface's own voice. */
export function Failed({
  title = "Could not load this",
  body,
  onRetry,
  className,
}: {
  title?: string;
  body?: string;
  onRetry?: () => void;
  className?: string;
}) {
  return (
    <div
      role="alert"
      className={cn("rounded border border-bad/30 bg-bad/[0.07] px-4 py-3", className)}
    >
      <p className="text-sm font-semibold text-bad">{title}</p>
      <p className="mt-0.5 text-sm text-text-2">
        {body ?? "The gateway did not respond. Check that the API is reachable."}
      </p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="mt-2 text-sm font-medium text-bad underline underline-offset-4 hover:no-underline"
        >
          Try again
        </button>
      )}
    </div>
  );
}
