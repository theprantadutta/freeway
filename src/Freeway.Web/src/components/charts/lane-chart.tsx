"use client";

import { useMemo, useState } from "react";
import { Table2, BarChart3 } from "lucide-react";
import { Segmented } from "@/components/ui/segmented";
import { LaneDot } from "@/components/charts/primitives";
import { columnPath, niceTicks } from "@/lib/charts";
import { useWidth } from "@/lib/hooks/motion";
import { LANES, LANE_ORDER, type LaneKey } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";
import { compact, count, money, shortDate } from "@/lib/utils/format";
import type { UsagePoint } from "@/lib/types";

type Metric = "requests" | "tokens" | "billed" | "credit";

const METRICS: { value: Metric; label: string }[] = [
  { value: "requests", label: "Requests" },
  { value: "tokens", label: "Tokens" },
  { value: "billed", label: "Billed" },
  { value: "credit", label: "Credit" },
];

const STACK: LaneKey[] = [...LANE_ORDER, "other"];
const M = { top: 16, right: 8, bottom: 30, left: 48 };
const GAP = 2;

/**
 * Traffic per day. Requests stack by lane, so the mix is readable as well as the
 * volume; the money and token views are a single series in ink. One measure at a
 * time on one axis -- never two scales sharing a plot.
 */
export function LaneChart({ series, height = 300 }: { series: UsagePoint[]; height?: number }) {
  const [metric, setMetric] = useState<Metric>("requests");
  const [view, setView] = useState<"chart" | "table">("chart");
  const [hover, setHover] = useState<number | null>(null);
  const [ref, width] = useWidth<HTMLDivElement>();

  const valueOf = (p: UsagePoint) =>
    metric === "requests"
      ? p.requests
      : metric === "tokens"
        ? p.tokens
        : metric === "billed"
          ? Number(p.cost)
          : Number(p.credit_usd ?? 0);

  const fmt = (v: number) => (metric === "billed" || metric === "credit" ? money(v) : compact(v));

  const lanesPresent = useMemo(
    () => STACK.filter((k) => series.some((p) => (p.lanes?.[k] ?? 0) > 0)),
    [series]
  );
  const laneTotals = useMemo(() => {
    const t: Partial<Record<LaneKey, number>> = {};
    for (const p of series) for (const k of STACK) t[k] = (t[k] ?? 0) + (p.lanes?.[k] ?? 0);
    return t;
  }, [series]);

  const values = series.map(valueOf);
  const total = values.reduce((a, b) => a + b, 0);
  const max = Math.max(...values, 0);
  const ticks = niceTicks(max || 1, 4);
  const top = ticks[ticks.length - 1] || 1;

  const innerW = Math.max(0, width - M.left - M.right);
  const innerH = height - M.top - M.bottom;
  const slot = series.length ? innerW / series.length : 0;
  const barW = Math.max(2, Math.min(24, slot * 0.64));
  const y = (v: number) => M.top + innerH - (v / top) * innerH;

  const labelEvery = Math.max(1, Math.ceil(series.length / Math.max(2, Math.floor(innerW / 90))));
  const active = hover != null ? series[hover] : null;

  return (
    <div>
      <div className="flex flex-wrap items-center justify-between gap-3 px-5 pt-5 sm:px-6">
        <div>
          <p className="text-lg font-bold text-text">
            {METRICS.find((m) => m.value === metric)!.label} per day
          </p>
          <p className="mt-0.5 text-sm text-text-3">
            <span className="tnum font-semibold text-text-2">{fmt(total)}</span> over {series.length} days
            {metric === "requests" && ", coloured by the lane each request asked for"}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Segmented size="sm" label="Measure" value={metric} onChange={setMetric} segments={METRICS} />
          <button
            onClick={() => setView(view === "chart" ? "table" : "chart")}
            className="grid h-9 w-9 place-items-center rounded-lg text-text-3 ring-1 ring-hair transition-colors hover:bg-raised hover:text-text"
            aria-label={view === "chart" ? "Show as a table" : "Show as a chart"}
            title={view === "chart" ? "Show as a table" : "Show as a chart"}
          >
            {view === "chart" ? <Table2 className="h-4 w-4" /> : <BarChart3 className="h-4 w-4" />}
          </button>
        </div>
      </div>

      {metric === "requests" && lanesPresent.length > 0 && (
        <ul className="flex flex-wrap gap-x-5 gap-y-2 px-5 pt-4 sm:px-6" aria-label="Lanes">
          {lanesPresent.map((k) => (
            <li key={k} className="flex items-center gap-2 text-sm text-text-2">
              <LaneDot scope={LANES[k].scope} />
              {LANES[k].label}
              <span className="tnum font-semibold text-text">{compact(laneTotals[k] ?? 0)}</span>
            </li>
          ))}
        </ul>
      )}

      {view === "table" ? (
        <div className="mt-4 max-h-[22rem] overflow-auto px-5 pb-5 sm:px-6">
          <table className="w-full text-sm">
            <thead className="sticky top-0 bg-panel text-left text-text-3">
              <tr>
                <th className="py-2 pr-4 font-semibold">Day</th>
                <th className="py-2 pr-4 text-right font-semibold">Requests</th>
                {lanesPresent.map((k) => (
                  <th key={k} className="py-2 pr-4 text-right font-semibold">{LANES[k].label}</th>
                ))}
                <th className="py-2 pr-4 text-right font-semibold">Tokens</th>
                <th className="py-2 pr-4 text-right font-semibold">Billed</th>
                <th className="py-2 text-right font-semibold">Credit</th>
              </tr>
            </thead>
            <tbody className="tnum divide-y divide-hair">
              {[...series].reverse().map((p) => (
                <tr key={p.date} className="text-text-2">
                  <td className="py-2 pr-4 text-text">{shortDate(p.date)}</td>
                  <td className="py-2 pr-4 text-right">{count(p.requests)}</td>
                  {lanesPresent.map((k) => (
                    <td key={k} className="py-2 pr-4 text-right">{count(p.lanes?.[k] ?? 0)}</td>
                  ))}
                  <td className="py-2 pr-4 text-right">{compact(p.tokens)}</td>
                  <td className="py-2 pr-4 text-right">{money(Number(p.cost))}</td>
                  <td className="py-2 text-right">{money(Number(p.credit_usd ?? 0))}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div ref={ref} className="relative mt-2 px-2 pb-3 sm:px-3" onMouseLeave={() => setHover(null)}>
          {width > 0 && (
            <svg width={width} height={height} className="block" role="img" aria-label={`${METRICS.find((m) => m.value === metric)!.label} per day`}>
              {ticks.map((t) => (
                <g key={t}>
                  <line x1={M.left} x2={width - M.right} y1={y(t)} y2={y(t)} stroke="rgb(var(--hair))" />
                  <text x={M.left - 10} y={y(t)} dy="0.32em" textAnchor="end" className="tnum fill-text-3 text-2xs">
                    {fmt(t)}
                  </text>
                </g>
              ))}

              <g key={`${metric}-${series.length}`}>
                {series.map((p, i) => {
                  const cx = M.left + slot * i + slot / 2;
                  const x = cx - barW / 2;
                  const delay = { ["--d" as string]: `${Math.min(i * 14, 600)}ms` };
                  const dim = hover != null && hover !== i;

                  if (metric !== "requests") {
                    const v = valueOf(p);
                    const h = M.top + innerH - y(v);
                    return (
                      <path
                        key={p.date}
                        d={columnPath(x, y(v), barW, h)}
                        className={cn("grow-y fill-text-2 transition-opacity duration-200", dim && "opacity-40")}
                        style={delay}
                      />
                    );
                  }

                  // Stacked by lane, bottom up, each segment parted by a surface gap.
                  let acc = 0;
                  const segs = STACK.map((k) => ({ k, v: p.lanes?.[k] ?? 0 })).filter((s) => s.v > 0);
                  return (
                    <g key={p.date} className={cn("grow-y transition-opacity duration-200", dim && "opacity-40")} style={delay}>
                      {segs.map((s, si) => {
                        const y0 = y(acc);
                        acc += s.v;
                        const y1 = y(acc);
                        const isTop = si === segs.length - 1;
                        const h = Math.max(0, y0 - y1 - (si > 0 ? GAP : 0));
                        const yy = y1;
                        return isTop ? (
                          <path key={s.k} d={columnPath(x, yy, barW, h)} className={cn(LANES[s.k].scope)} fill="rgb(var(--lane))" />
                        ) : (
                          <rect key={s.k} x={x} y={yy} width={barW} height={h} className={LANES[s.k].scope} fill="rgb(var(--lane))" />
                        );
                      })}
                    </g>
                  );
                })}
              </g>

              <line x1={M.left} x2={width - M.right} y1={M.top + innerH} y2={M.top + innerH} stroke="rgb(var(--hair-bright))" />

              {series.map((p, i) =>
                i % labelEvery === 0 || i === series.length - 1 ? (
                  <text
                    key={p.date}
                    x={M.left + slot * i + slot / 2}
                    y={height - 8}
                    textAnchor="middle"
                    className="fill-text-3 text-2xs"
                  >
                    {shortDate(p.date)}
                  </text>
                ) : null
              )}

              {/* Hit targets: the whole day's column, not just the painted bar. */}
              {series.map((p, i) => (
                <rect
                  key={`hit-${p.date}`}
                  x={M.left + slot * i}
                  y={M.top}
                  width={slot}
                  height={innerH}
                  fill={hover === i ? "rgb(var(--text) / 0.04)" : "transparent"}
                  onMouseEnter={() => setHover(i)}
                  onFocus={() => setHover(i)}
                  tabIndex={-1}
                />
              ))}
            </svg>
          )}

          {active && hover != null && (
            <ChartTip x={M.left + slot * hover + slot / 2 + 12} width={width}>
              <p className="text-sm font-semibold text-text-2">{shortDate(active.date)}</p>
              <p className="tnum mt-1 text-xl font-bold text-text">{fmt(valueOf(active))}</p>
              {metric === "requests" && (
                <ul className="mt-2 space-y-1">
                  {STACK.filter((k) => (active.lanes?.[k] ?? 0) > 0).reverse().map((k) => (
                    <li key={k} className="flex items-center gap-2 text-sm">
                      <span className={cn(LANES[k].scope, "h-0.5 w-3 rounded-full lane-bg")} aria-hidden />
                      <span className="tnum w-10 font-bold text-text">{count(active.lanes?.[k] ?? 0)}</span>
                      <span className="text-text-3">{LANES[k].label}</span>
                    </li>
                  ))}
                </ul>
              )}
              {metric === "requests" && active.failures > 0 && (
                <p className="mt-2 text-sm text-bad">{count(active.failures)} failed</p>
              )}
            </ChartTip>
          )}
        </div>
      )}
    </div>
  );
}

/** A tooltip that stays inside the chart, flipping to the left near the right edge. */
export function ChartTip({ x, width, children }: { x: number; width: number; children: React.ReactNode }) {
  const flip = x > width - 220;
  return (
    <div
      className="pointer-events-none absolute top-4 z-20 min-w-[11rem] rounded-xl border border-hair-bright bg-overlay px-4 py-3 shadow-overlay animate-pop"
      style={flip ? { right: width - x + 24 } : { left: x }}
    >
      {children}
    </div>
  );
}
