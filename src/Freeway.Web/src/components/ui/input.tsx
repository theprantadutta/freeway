import { forwardRef, InputHTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils/cn";

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  hint?: string;
  /** Rendered inside the field, before the text. */
  leading?: ReactNode;
}

const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, label, error, hint, leading, id, ...props }, ref) => {
    const inputId = id || label?.toLowerCase().replace(/\s/g, "-");
    const describedBy = error
      ? `${inputId}-error`
      : hint
        ? `${inputId}-hint`
        : undefined;

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={inputId}
            className="block text-sm font-medium text-ink mb-1.5"
          >
            {label}
          </label>
        )}
        <div className="relative">
          {leading && (
            <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-subtle">
              {leading}
            </span>
          )}
          <input
            ref={ref}
            id={inputId}
            aria-invalid={error ? true : undefined}
            aria-describedby={describedBy}
            className={cn(
              "h-9 w-full rounded-control border bg-panel px-3 text-sm text-ink",
              "border-line-strong placeholder:text-subtle",
              "transition-[border-color,box-shadow] duration-150 ease-swift",
              "hover:border-subtle",
              "focus:outline-none focus:border-brand focus:ring-2 focus:ring-brand/20",
              "disabled:bg-inset disabled:text-subtle disabled:cursor-not-allowed",
              leading && "pl-9",
              error && "border-danger focus:border-danger focus:ring-danger/20",
              className
            )}
            {...props}
          />
        </div>
        {error ? (
          <p id={`${inputId}-error`} className="mt-1.5 text-xs text-danger">
            {error}
          </p>
        ) : hint ? (
          <p id={`${inputId}-hint`} className="mt-1.5 text-xs text-subtle">
            {hint}
          </p>
        ) : null}
      </div>
    );
  }
);
Input.displayName = "Input";

export { Input };
