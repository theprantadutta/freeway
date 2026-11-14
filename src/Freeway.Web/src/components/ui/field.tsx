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
          <label htmlFor={fieldId} className="mb-2 block text-sm font-semibold text-text-2">
            {label}
          </label>
        )}
        <div className="relative">
          {lead && (
            <span className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-text-3">
              {lead}
            </span>
          )}
          <input
            ref={ref}
            id={fieldId}
            aria-invalid={error ? true : undefined}
            aria-describedby={describedBy}
            className={cn(
              "h-11 w-full rounded-[0.625rem] border border-hair-bright bg-sunken px-3.5 text-base text-text",
              "placeholder:text-text-3",
              "transition-[border-color,background-color,box-shadow] duration-150 ease-out",
              "hover:border-text-3/60",
              "focus:border-text-2 focus:bg-panel focus:shadow-[0_0_0_4px_rgb(var(--text)/0.06)] focus:outline-none",
              "disabled:opacity-50",
              lead && "pl-10",
              error && "border-bad focus:border-bad",
              className
            )}
            {...props}
          />
        </div>
        {error ? (
          <p id={`${fieldId}-err`} className="mt-2 text-sm text-bad">
            {error}
          </p>
        ) : hint ? (
          <p id={`${fieldId}-hint`} className="mt-2 text-sm text-text-3">
            {hint}
          </p>
        ) : null}
      </div>
    );
  }
);
Field.displayName = "Field";

export { Field };
