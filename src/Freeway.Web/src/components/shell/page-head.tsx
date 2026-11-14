import { ReactNode } from "react";
import { cn } from "@/lib/utils/cn";

/**
 * The top of a page: a real display heading, a sentence saying what the page is
 * for, and the page's own controls on the same line.
 */
export function PageHead({
  title,
  lede,
  actions,
  className,
}: {
  title: ReactNode;
  lede?: ReactNode;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("reveal flex flex-wrap items-end justify-between gap-x-8 gap-y-5", className)}>
      <div className="min-w-0 max-w-3xl">
        <h1 className="text-3xl font-extrabold text-text sm:text-4xl">{title}</h1>
        {lede && <p className="mt-2 text-lg text-text-2">{lede}</p>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-3">{actions}</div>}
    </div>
  );
}
