import { ReactNode } from "react";
import { cn } from "@/lib/utils/cn";

/**
 * The top of a page: a real display heading, then the page's own controls.
 *
 * The previous header set the page title at 18px, barely above body text, so nothing
 * anchored the eye on arrival. It gets a display size here and the controls sit on
 * the same line rather than in a separate bar.
 */
export function PageHead({
  title,
  lede,
  actions,
  className,
}: {
  title: string;
  lede?: ReactNode;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("flex flex-wrap items-end justify-between gap-x-6 gap-y-4", className)}>
      <div className="min-w-0">
        <h1 className="text-3xl font-semibold tracking-tight text-text">{title}</h1>
        {lede && <p className="mt-1.5 max-w-xl text-sm text-text-2">{lede}</p>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
