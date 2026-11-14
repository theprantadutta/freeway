import { ButtonHTMLAttributes, forwardRef } from "react";
import { Loader2 } from "lucide-react";
import { cn } from "@/lib/utils/cn";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "solid" | "outline" | "ghost" | "danger" | "lane";
  size?: "sm" | "md" | "lg" | "icon";
  busy?: boolean;
}

/** Presses in slightly when clicked, so a click is felt before its result arrives. */
const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = "outline", size = "md", busy, disabled, children, ...props }, ref) => {
    const variants = {
      solid: "grad text-white shadow-[0_8px_24px_-10px_rgb(var(--grad-b)/0.7)] hover:brightness-110",
      outline: "border border-hair-bright bg-raised text-text hover:border-text-3/70 hover:bg-raised/70",
      ghost: "text-text-2 hover:bg-raised hover:text-text",
      danger: "border border-bad/40 bg-bad/10 text-bad hover:bg-bad/20",
      lane: "border lane-edge lane-soft text-text hover:brightness-125",
    };

    const sizes = {
      sm: "h-8 px-3 text-sm gap-1.5 rounded-lg",
      md: "h-10 px-4 text-base gap-2 rounded-[0.625rem]",
      lg: "h-12 px-5 text-lg gap-2 rounded-xl",
      icon: "h-10 w-10 rounded-[0.625rem]",
    };

    return (
      <button
        ref={ref}
        className={cn(
          "inline-flex items-center justify-center whitespace-nowrap font-semibold",
          "transition-[background-color,border-color,opacity,filter,transform] duration-150 ease-out",
          "active:scale-[0.97] disabled:pointer-events-none disabled:opacity-40",
          variants[variant],
          sizes[size],
          className
        )}
        disabled={disabled || busy}
        {...props}
      >
        {busy && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
        {children}
      </button>
    );
  }
);
Button.displayName = "Button";

export { Button };
