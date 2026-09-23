import { cn } from "@/lib/utils/cn";

/**
 * Five lanes of decreasing width, in cost order, inside a rounded plate.
 *
 * The product routes a request down one of these lanes, so the mark is the routing
 * table rather than an abstract glyph. Widths step down because cheaper lanes carry
 * more traffic — the shape says what the product does.
 */
export function Mark({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 32 32"
      className={cn("h-full w-full", className)}
      role="img"
      aria-label="Freeway"
    >
      <rect width="32" height="32" rx="8" fill="rgb(var(--raised))" />
      <rect x="0.5" y="0.5" width="31" height="31" rx="7.5" fill="none" stroke="rgb(var(--hair-bright))" />
      <rect x="7" y="7"  width="18" height="3" rx="1.5" fill="rgb(var(--free))" />
      <rect x="7" y="12" width="14" height="3" rx="1.5" fill="rgb(var(--low))" />
      <rect x="7" y="17" width="10" height="3" rx="1.5" fill="rgb(var(--moderate))" />
      <rect x="7" y="22" width="6"  height="3" rx="1.5" fill="rgb(var(--premium))" />
    </svg>
  );
}

/** Mark plus wordmark, for the console header and the sign-in page. */
export function Wordmark({ className }: { className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-2.5", className)}>
      <span className="h-7 w-7 shrink-0">
        <Mark />
      </span>
      <span className="text-lg font-semibold tracking-tight text-text">Freeway</span>
    </span>
  );
}
