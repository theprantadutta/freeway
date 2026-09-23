import { forwardRef, ButtonHTMLAttributes } from "react";
import { Loader2 } from "lucide-react";
import { cn } from "@/lib/utils/cn";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "outline" | "ghost" | "danger" | "accent";
  size?: "sm" | "md" | "lg" | "icon";
  isLoading?: boolean;
}

const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  (
    { className, variant = "primary", size = "md", isLoading, disabled, children, ...props },
    ref
  ) => {
    const variants = {
      primary: "bg-brand text-white hover:brightness-110 active:brightness-95",
      accent: "bg-accent text-white hover:brightness-110 active:brightness-95",
      secondary: "bg-inset text-ink hover:bg-line",
      outline: "border border-line-strong bg-panel text-ink hover:bg-inset",
      ghost: "bg-transparent text-muted hover:bg-inset hover:text-ink",
      danger: "bg-danger text-white hover:brightness-110 active:brightness-95",
    };

    const sizes = {
      sm: "h-8 px-3 text-xs gap-1.5",
      md: "h-9 px-3.5 text-sm gap-2",
      lg: "h-11 px-5 text-base gap-2",
      icon: "h-9 w-9",
    };

    return (
      <button
        ref={ref}
        className={cn(
          "inline-flex items-center justify-center whitespace-nowrap rounded-control font-medium",
          "transition-[background-color,filter,opacity] duration-150 ease-swift",
          "disabled:opacity-50 disabled:pointer-events-none",
          variants[variant],
          sizes[size],
          className
        )}
        disabled={disabled || isLoading}
        {...props}
      >
        {isLoading && <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />}
        {children}
      </button>
    );
  }
);
Button.displayName = "Button";

export { Button };
