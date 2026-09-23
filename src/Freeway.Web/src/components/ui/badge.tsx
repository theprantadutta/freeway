import { HTMLAttributes, forwardRef } from "react";
import { cn } from "@/lib/utils/cn";

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?:
    | "default"
    | "success"
    | "warning"
    | "error"
    | "info"
    | "primary"
    | "accent"
    | "outline";
  size?: "sm" | "md";
  /** Shows a filled dot in the badge colour before the label. */
  dot?: boolean;
}

const Badge = forwardRef<HTMLSpanElement, BadgeProps>(
  ({ className, variant = "default", size = "sm", dot, children, ...props }, ref) => {
    const variants = {
      default: "bg-inset text-muted",
      success: "bg-ok/12 text-ok",
      warning: "bg-warn/12 text-warn",
      error: "bg-danger/12 text-danger",
      info: "bg-info/12 text-info",
      primary: "bg-brand/12 text-brand",
      accent: "accent-tint accent-text",
      outline: "border border-line text-muted",
    };

    const sizes = {
      sm: "h-5 px-2 text-micro gap-1.5",
      md: "h-6 px-2.5 text-xs gap-1.5",
    };

    return (
      <span
        ref={ref}
        className={cn(
          "inline-flex items-center font-medium rounded-full whitespace-nowrap",
          variants[variant],
          sizes[size],
          className
        )}
        {...props}
      >
        {dot && (
          <span
            className="h-1.5 w-1.5 rounded-full bg-current shrink-0"
            aria-hidden
          />
        )}
        {children}
      </span>
    );
  }
);
Badge.displayName = "Badge";

export { Badge };
