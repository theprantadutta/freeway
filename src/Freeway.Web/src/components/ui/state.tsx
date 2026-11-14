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
    <div className={cn("flex flex-col items-center justify-center px-6 py-16 text-center", className)}>
      <span className="grid h-12 w-12 place-items-center rounded-xl bg-sunken text-text-2 ring-1 ring-hair">
        <Icon className="h-5 w-5" aria-hidden />
      </span>
      <h3 className="mt-5 text-lg font-bold text-text">{title}</h3>
      <p className="mt-1.5 max-w-md text-base text-text-2">{body}</p>
      {action && <div className="mt-6">{action}</div>}
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
    <div role="alert" className={cn("rounded-panel border border-bad/30 bg-bad/[0.07] px-5 py-4", className)}>
      <p className="text-base font-bold text-text">{title}</p>
      <p className="mt-1 text-base text-text-2">
        {body ?? "The gateway did not respond. Check that the API is reachable."}
      </p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="mt-2 text-base font-semibold text-text underline underline-offset-4 hover:no-underline"
        >
          Try again
        </button>
      )}
    </div>
  );
}
