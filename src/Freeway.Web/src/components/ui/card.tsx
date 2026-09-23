import { HTMLAttributes, forwardRef } from "react";
import { cn } from "@/lib/utils/cn";

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  hover?: boolean;
  /** Draws the current accent scope as a left rail. */
  rail?: boolean;
}

const Card = forwardRef<HTMLDivElement, CardProps>(
  ({ className, hover, rail, ...props }, ref) => {
    return (
      <div
        ref={ref}
        className={cn(
          "bg-panel rounded-panel border border-line",
          rail && "rail",
          hover &&
            "transition-[border-color,transform] duration-200 ease-swift hover:border-line-strong hover:-translate-y-px",
          className
        )}
        {...props}
      />
    );
  }
);
Card.displayName = "Card";

const CardHeader = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        "flex items-center justify-between gap-4 px-5 py-4 border-b border-line",
        className
      )}
      {...props}
    />
  )
);
CardHeader.displayName = "CardHeader";

const CardTitle = forwardRef<HTMLHeadingElement, HTMLAttributes<HTMLHeadingElement>>(
  ({ className, ...props }, ref) => (
    <h2
      ref={ref}
      className={cn("text-base font-semibold text-ink", className)}
      {...props}
    />
  )
);
CardTitle.displayName = "CardTitle";

const CardContent = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn("p-5", className)} {...props} />
  )
);
CardContent.displayName = "CardContent";

const CardFooter = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn("px-5 py-4 border-t border-line", className)}
      {...props}
    />
  )
);
CardFooter.displayName = "CardFooter";

export { Card, CardHeader, CardTitle, CardContent, CardFooter };
