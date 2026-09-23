"use client";

import { useState } from "react";
import { cn } from "@/lib/utils/cn";
import { compact, money, pct } from "@/lib/utils/format";
import { LANES, type LaneKey } from "@/lib/theme/lanes";
import type { LaneUsage } from "@/lib/types";

/**
 * How traffic divided across the lanes, as one stacked bar plus a keyed list.
 *
 * A pie would be the obvious choice and the wrong one: the job here is comparing a
 * handful of parts to the whole, which a single bar does at a glance and without
 * asking anyone to judge angles. Every segment is keyed by name in the list below,
 * so hue is never the only thing carrying identity.
 */
export function LaneSplit({
  lanes,
  metric = "requests",
  className,
}: {
  lanes: LaneUsage[];
  metric?: "requests" | "cost";
  className?: string;
}) {
  const [hover, setHover] = useState<string | null>(null);

  const value = (l: LaneUsage) => (metric === "cost" ? Number(l.cost) : l.requests);
  const total = lanes.reduce((sum, l) => sum + value(l), 0);

  if (total === 0) {
    return (
      <p className={cn("px-4 py-6 text-sm text-text-3", className)}>
        No traffic in this window yet.
      </p>
    );
  }

  const fmt = (v: number) => (metric === "cost" ? money(v) : compact(v));

  return (
    <div className={cn("px-4 pb-4", className)}>
      <div
        className="flex h-2.5 w-full gap-px overflow-hidden rounded-full"
        onMouseLeave={() => setHover(null)}
      >
        {lanes.map((l) => {
          const lane = LANES[(l.lane as LaneKey) in LANES ? (l.lane as LaneKey) : "other"];
          const share = (value(l) / total) * 100;
          if (share <= 0) return null;
          return (
            <span
              key={l.lane}
              className={cn(
                lane.scope,
                "lane-bg h-full transition-opacity duration-150",
                hover && hover !== l.lane ? "opacity-35" : "opacity-100"
              )}
              style={{ width: `${share}%` }}
              onMouseEnter={() => setHover(l.lane)}
              title={`${lane.label}: ${fmt(value(l))}`}
            />
          );
        })}
      </div>

      <ul className="mt-3 grid gap-x-5 gap-y-1.5 sm:grid-cols-2">
        {lanes.map((l) => {
          const lane = LANES[(l.lane as LaneKey) in LANES ? (l.lane as LaneKey) : "other"];
          const share = (value(l) / total) * 100;
          return (
            <li
              key={l.lane}
              className={cn(
                lane.scope,
                "flex items-center gap-2 rounded px-1 py-0.5 transition-colors",
                hover === l.lane && "bg-raised"
              )}
              onMouseEnter={() => setHover(l.lane)}
              onMouseLeave={() => setHover(null)}
            >
              <span className="h-2 w-2 shrink-0 rounded-full lane-bg" aria-hidden />
              <span className="flex-1 truncate text-sm text-text-2">{lane.label}</span>
              <span className="fig text-sm text-text">{fmt(value(l))}</span>
              <span className="fig w-12 text-right text-xs text-text-3">{pct(share)}</span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
