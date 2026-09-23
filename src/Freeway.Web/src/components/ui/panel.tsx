import { HTMLAttributes, ReactNode, forwardRef } from "react";
import { cn } from "@/lib/utils/cn";

/**
 * A hairline box lifted out of the field. Deliberately no shadow: shadows imply
 * paper, and this is a screen showing live numbers.
 */
const Panel = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn("panel overflow-hidden", className)} {...props} />
  )
);
Panel.displayName = "Panel";

function PanelHead({
  title,
  meta,
  action,
  className,
}: {
  title: ReactNode;
  meta?: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex items-center justify-between gap-4 border-b border-hair px-4 py-3",
        className
      )}
    >
      <div className="min-w-0">
        <h2 className="truncate text-sm font-semibold text-text">{title}</h2>
        {meta && <p className="mt-0.5 truncate text-xs text-text-3">{meta}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

function PanelBody({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("p-4", className)} {...props} />;
}

/**
 * A section marker: a small label sitting on a hairline that runs to the edge.
 * Sections are announced by structure here rather than by another bold heading.
 */
function Rule({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="mb-3 flex items-center gap-3">
      <div className="rule flex-1">
        <span className="text-2xs font-medium uppercase text-text-3">{children}</span>
      </div>
      {action}
    </div>
  );
}

export { Panel, PanelHead, PanelBody, Rule };
