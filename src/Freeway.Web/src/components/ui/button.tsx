import { ButtonHTMLAttributes, forwardRef } from "react";
import { Loader2 } from "lucide-react";
import { cn } from "@/lib/utils/cn";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "solid" | "outline" | "ghost" | "danger" | "lane";
  size?: "sm" | "md" | "lg" | "icon";
  busy?: boolean;
}

const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = "outline", size = "md", busy, disabled, children, ...props }, ref) => {
    const variants = {
      solid: "bg-text text-base hover:opacity-90",
      outline: "border border-hair-bright bg-raised text-text hover:border-text-3",
      ghost: "text-text-2 hover:bg-raised hover:text-text",
      danger: "border border-bad/40 bg-bad/10 text-bad hover:bg-bad/20",
      lane: "border lane-edge lane-soft lane-fg hover:brightness-125",
    };

    const sizes = {
      sm: "h-7 px-2.5 text-xs gap-1.5",
      md: "h-8 px-3 text-sm gap-1.5",
      lg: "h-10 px-4 text-base gap-2",
      icon: "h-8 w-8",
    };

    return (
      <button
        ref={ref}
        className={cn(
          "inline-flex items-center justify-center whitespace-nowrap rounded font-medium",
          "transition-[background-color,border-color,opacity,filter] duration-150 ease-out",
          "disabled:opacity-40 disabled:pointer-events-none",
          variants[variant],
          sizes[size],
          className
        )}
        disabled={disabled || busy}
        {...props}
      >
        {busy && <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />}
        {children}
      </button>
    );
  }
);
Button.displayName = "Button";

export { Button };
