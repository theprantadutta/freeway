import { cn } from "@/lib/utils/cn";

/**
 * A bare trend shape for a table row. No axes, no labels: it answers "is this rising
 * or flat" beside a number that already gives the magnitude.
 */
export function Spark({
  values,
  className,
}: {
  values: number[];
  className?: string;
}) {
  if (values.length === 0) return null;
  const max = Math.max(...values, 1);

  return (
    <span
      className={cn("inline-flex h-6 items-end gap-px", className)}
      aria-hidden
    >
      {values.map((v, i) => (
        <span
          key={i}
          className={cn("w-0.5 rounded-sm", v === 0 ? "bg-hair" : "bg-lane")}
          style={{ height: v === 0 ? "2px" : `max(2px, ${(v / max) * 100}%)` }}
        />
      ))}
    </span>
  );
}
