import { InputHTMLAttributes, ReactNode, forwardRef } from "react";
import { cn } from "@/lib/utils/cn";

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  hint?: string;
  lead?: ReactNode;
}

const Field = forwardRef<HTMLInputElement, FieldProps>(
  ({ className, label, error, hint, lead, id, ...props }, ref) => {
    const fieldId = id || label?.toLowerCase().replace(/\s/g, "-");
    const describedBy = error ? `${fieldId}-err` : hint ? `${fieldId}-hint` : undefined;

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={fieldId}
            className="mb-1.5 block text-2xs font-medium uppercase text-text-3"
          >
            {label}
          </label>
        )}
        <div className="relative">
          {lead && (
            <span className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-text-3">
              {lead}
            </span>
          )}
          <input
            ref={ref}
            id={fieldId}
            aria-invalid={error ? true : undefined}
            aria-describedby={describedBy}
            className={cn(
              "h-9 w-full rounded border border-hair-bright bg-sunken px-3 text-sm text-text",
              "placeholder:text-text-3",
              "transition-[border-color,background-color] duration-150 ease-out",
              "hover:border-text-3/60",
              "focus:border-accent focus:bg-panel focus:outline-none",
              "disabled:opacity-50",
              lead && "pl-8",
              error && "border-bad focus:border-bad",
              className
            )}
            {...props}
          />
        </div>
        {error ? (
          <p id={`${fieldId}-err`} className="mt-1.5 text-xs text-bad">
            {error}
          </p>
        ) : hint ? (
          <p id={`${fieldId}-hint`} className="mt-1.5 text-xs text-text-3">
            {hint}
          </p>
        ) : null}
      </div>
    );
  }
);
Field.displayName = "Field";

export { Field };
