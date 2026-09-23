"use client";

import { useMemo, useState } from "react";
import { cn } from "@/lib/utils/cn";
import { compact, money, shortDate } from "@/lib/utils/format";
import type { UsagePoint } from "@/lib/types";

export type Metric = "requests" | "cost" | "tokens";

const METRICS: { key: Metric; label: string }[] = [
  { key: "requests", label: "Requests" },
  { key: "cost", label: "Cost" },
  { key: "tokens", label: "Tokens" },
];

/**
 * Daily volume, one bar per day.
 *
 * Bars rather than an area: these are counts in discrete daily buckets, and an area
 * would imply a continuous quantity that was sampled. Days with no traffic keep their
 * slot and render as a baseline tick, so a gap reads as "nothing happened" instead of
 * as missing data.
 *
 * One measure is shown at a time. Two measures of different scale would need two
 * y-axes, which makes the relationship between the series arbitrary.
 */
export function SeriesChart({
  points,
  className,
}: {
  points: UsagePoint[];
  className?: string;
}) {
  const [metric, setMetric] = useState<Metric>("requests");
  const [hover, setHover] = useState<number | null>(null);

  const values = useMemo(
    () =>
      points.map((p) =>
        metric === "requests" ? p.requests : metric === "cost" ? Number(p.cost) : p.tokens
      ),
    [points, metric]
  );

  const max = Math.max(...values, 1);
  const total = values.reduce((a, b) => a + b, 0);
  const peakIndex = values.indexOf(Math.max(...values));

  const fmt = (v: number) =>
    metric === "cost" ? money(v) : compact(v);

  const active = hover ?? null;
  const activePoint = active != null ? points[active] : null;

  return (
    <div className={cn("flex flex-col", className)}>
      <div className="flex flex-wrap items-end justify-between gap-3 px-4 pt-4">
        <div>
          <p className="text-2xs font-medium uppercase text-text-3">
            {METRICS.find((m) => m.key === metric)!.label} per day
          </p>
          <p className="fig mt-1 text-2xl font-semibold text-text">{fmt(total)}</p>
          <p className="mt-0.5 text-xs text-text-3">
            across {points.length} days · peak {fmt(values[peakIndex] ?? 0)} on{" "}
            {points[peakIndex] ? shortDate(points[peakIndex].date) : "—"}
          </p>
        </div>

        <div
          role="tablist"
          aria-label="Chart measure"
          className="inline-flex gap-0.5 rounded bg-sunken p-0.5"
        >
          {METRICS.map((m) => (
            <button
              key={m.key}
              role="tab"
              aria-selected={metric === m.key}
              onClick={() => setMetric(m.key)}
              className={cn(
                "rounded-[0.3125rem] px-2.5 py-1 text-xs font-medium transition-colors duration-150 ease-out",
                metric === m.key ? "bg-raised text-text" : "text-text-3 hover:text-text-2"
              )}
            >
              {m.label}
            </button>
          ))}
        </div>
      </div>

      <div className="relative mt-4 px-4">
        {/* Readout sits above the plot so the bars never jump to make room. */}
        <div className="mb-2 h-9">
          {activePoint ? (
            <div className="animate-pop">
              <p className="fig text-lg font-semibold text-text">
                {fmt(values[active!])}
              </p>
              <p className="text-xs text-text-3">
                {shortDate(activePoint.date)}
                {activePoint.failures > 0 && (
                  <span className="ml-2 text-bad">{activePoint.failures} failed</span>
                )}
              </p>
            </div>
          ) : (
            <p className="text-xs text-text-3">Hover a day for its figures</p>
          )}
        </div>

        <div
          className="trace flex h-36 items-end gap-px"
          onMouseLeave={() => setHover(null)}
          role="img"
          aria-label={`${metric} per day over the last ${points.length} days`}
        >
          {points.map((p, i) => {
            const v = values[i];
            const pct = max === 0 ? 0 : (v / max) * 100;
            const isActive = active === i;
            return (
              <button
                key={p.date}
                onMouseEnter={() => setHover(i)}
                onFocus={() => setHover(i)}
                onBlur={() => setHover(null)}
                aria-label={`${shortDate(p.date)}: ${fmt(v)}`}
                className="group relative flex h-full flex-1 items-end outline-none"
              >
                {/* Full-height hit target: the bars themselves are too thin to aim at. */}
                <span
                  className={cn(
                    "absolute inset-0 rounded-sm transition-colors duration-100",
                    isActive ? "bg-text/[0.06]" : "bg-transparent"
                  )}
                  aria-hidden
                />
                <span
                  className={cn(
                    "relative w-full rounded-sm transition-[height,background-color] duration-300 ease-out",
                    v === 0
                      ? "bg-hair"
                      : isActive
                        ? "bg-accent"
                        : "bg-accent/45 group-hover:bg-accent/70"
                  )}
                  style={{ height: v === 0 ? "2px" : `max(3px, ${pct}%)` }}
                  aria-hidden
                />
              </button>
            );
          })}
        </div>

        <div className="mt-2 flex justify-between border-t border-hair pt-2 text-2xs text-text-3">
          <span>{points[0] ? shortDate(points[0].date) : ""}</span>
          <span>
            {points[Math.floor(points.length / 2)]
              ? shortDate(points[Math.floor(points.length / 2)].date)
              : ""}
          </span>
          <span>{points.at(-1) ? shortDate(points.at(-1)!.date) : ""}</span>
        </div>
      </div>
    </div>
  );
}
