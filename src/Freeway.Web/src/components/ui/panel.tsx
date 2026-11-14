import { HTMLAttributes, ReactNode, forwardRef } from "react";
import { cn } from "@/lib/utils/cn";

/** A card lifted off the road surface. See `.card` in globals.css. */
const Panel = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn("card overflow-hidden", className)} {...props} />
  )
);
Panel.displayName = "Panel";

function PanelHead({
  title,
  meta,
  action,
  icon,
  className,
}: {
  title: ReactNode;
  meta?: ReactNode;
  action?: ReactNode;
  icon?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("flex items-start justify-between gap-4 px-5 pb-1 pt-5 sm:px-6", className)}>
      <div className="flex min-w-0 items-start gap-3">
        {icon && (
          <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-sunken text-text-2 ring-1 ring-hair">
            {icon}
          </span>
        )}
        <div className="min-w-0">
          <h2 className="text-lg font-bold text-text">{title}</h2>
          {meta && <p className="mt-0.5 text-sm text-text-3">{meta}</p>}
        </div>
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

function PanelBody({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("px-5 pb-5 pt-4 sm:px-6", className)} {...props} />;
}

/** A section heading with its controls on the same line. */
function Rule({
  children,
  action,
  description,
}: {
  children: ReactNode;
  action?: ReactNode;
  description?: ReactNode;
}) {
  return (
    <div className="mb-4 flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
      <div className="min-w-0">
        <h2 className="text-xl font-bold text-text">{children}</h2>
        {description && <p className="mt-1 max-w-2xl text-base text-text-3">{description}</p>}
      </div>
      {action}
    </div>
  );
}

export { Panel, PanelHead, PanelBody, Rule };
