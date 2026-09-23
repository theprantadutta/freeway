import { cn } from "@/lib/utils/cn";

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn("shimmer rounded-control", className)} aria-hidden />;
}

export function SkeletonCard() {
  return (
    <div className="p-5 bg-panel rounded-panel border border-line">
      <div className="flex items-center gap-3">
        <Skeleton className="h-9 w-9 rounded-control" />
        <div className="space-y-2 flex-1">
          <Skeleton className="h-3.5 w-1/3" />
          <Skeleton className="h-3 w-1/5" />
        </div>
      </div>
      <Skeleton className="h-16 w-full mt-4" />
    </div>
  );
}

export function SkeletonList({ count = 3 }: { count?: number }) {
  return (
    <div className="space-y-3" role="status" aria-label="Loading">
      {Array.from({ length: count }).map((_, i) => (
        <SkeletonCard key={i} />
      ))}
    </div>
  );
}

/** Matching placeholder for the stat strip, so the layout does not jump. */
export function SkeletonStrip({ count = 4 }: { count?: number }) {
  return (
    <div className="bg-panel rounded-panel border border-line grid grid-cols-2 lg:grid-cols-4 divide-x divide-y lg:divide-y-0 divide-line">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="p-5 space-y-3">
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-7 w-24" />
        </div>
      ))}
    </div>
  );
}
