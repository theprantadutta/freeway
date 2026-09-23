import { CSSProperties } from "react";
import { cn } from "@/lib/utils/cn";

export function Pulse({
  className,
  style,
}: {
  className?: string;
  style?: CSSProperties;
}) {
  return <div className={cn("pulse rounded", className)} style={style} aria-hidden />;
}

export function PulseRows({ rows = 4, className }: { rows?: number; className?: string }) {
  return (
    <div className={cn("space-y-px", className)} role="status" aria-label="Loading">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex items-center gap-3 px-4 py-3">
          <Pulse className="h-7 w-7 shrink-0" />
          <Pulse className="h-3 flex-1" />
          <Pulse className="h-3 w-16 shrink-0" />
          <Pulse className="h-3 w-12 shrink-0" />
        </div>
      ))}
    </div>
  );
}
