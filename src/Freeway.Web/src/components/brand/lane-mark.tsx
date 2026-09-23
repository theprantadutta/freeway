import { cn } from "@/lib/utils/cn";

/**
 * Four lanes, one per model type, in cost order. The product routes requests
 * down these lanes, so the mark is the routing table rather than an abstract glyph.
 */
export function LaneMark({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className={cn("h-full w-full", className)}
      role="img"
      aria-label="Freeway"
    >
      <rect x="2" y="3" width="20" height="3.5" rx="1.75" fill="rgb(var(--free))" />
      <rect x="2" y="8" width="15" height="3.5" rx="1.75" fill="rgb(var(--low))" />
      <rect x="2" y="13" width="10" height="3.5" rx="1.75" fill="rgb(var(--moderate))" />
      <rect x="2" y="18" width="6" height="3.5" rx="1.75" fill="rgb(var(--premium))" />
    </svg>
  );
}
