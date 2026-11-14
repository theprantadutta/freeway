"use client";

import { useState } from "react";
import { monotonePath, niceTicks, type Pt } from "@/lib/charts";
import { useWidth } from "@/lib/hooks/motion";
import { cents, money, shortDate } from "@/lib/utils/format";
import type { CreditOverview } from "@/lib/types";

const M = { top: 20, right: 16, bottom: 30, left: 56 };

/**
 * The credit cycle as one line: what has been spent so far, adding up day by day,
 * and where the current pace lands by the reset (dashed, because it is a forecast).
 * The hairline across is where Freeway stops using the credit.
 */
export function CreditBurn({ c, height = 280 }: { c: CreditOverview; height?: number }) {
  const [ref, width] = useWidth<HTMLDivElement>();
  const [hover, setHover] = useState<number | null>(null);
  const grant = c.grant;
  if (!grant) return null;

  const totalDays = Math.max(1, Math.round(c.days_total));
  let acc = 0;
  const cumulative = c.series.map((d) => (acc += Number(d.credit_usd)));
  const spentSoFar = Number(c.used_usd);

  const projectedEnd = Number(c.projected_usd);
  const stopAt = Number(c.usable_usd);
  const top = niceTicks(Math.max(projectedEnd, spentSoFar, stopAt * 0.25, 1), 4);
  const yMax = top[top.length - 1] || 1;

  const innerW = Math.max(0, width - M.left - M.right);
  const innerH = height - M.top - M.bottom;
  const x = (i: number) => M.left + (i / totalDays) * innerW;
  const y = (v: number) => M.top + innerH - (Math.min(v, yMax) / yMax) * innerH;

  const pts: Pt[] = cumulative.map((v, i) => [x(i + 1), y(v)]);
  const line = monotonePath([[x(0), y(0)], ...pts]);
  const lastX = pts.length ? pts[pts.length - 1][0] : x(0);
  const lastY = pts.length ? pts[pts.length - 1][1] : y(0);
  const area = `${line}L${lastX},${y(0)}L${x(0)},${y(0)}Z`;
  const stopVisible = stopAt <= yMax;

  const step = innerW / totalDays;
  const active = hover != null && hover < cumulative.length ? hover : null;

  return (
    <div ref={ref} className="relative px-2 pb-2 sm:px-3" onMouseLeave={() => setHover(null)}>
      {width > 0 && (
        <svg width={width} height={height} role="img" aria-label="Credit spent over the cycle">
          {top.map((t) => (
            <g key={t}>
              <line x1={M.left} x2={width - M.right} y1={y(t)} y2={y(t)} stroke="rgb(var(--hair))" />
              <text x={M.left - 10} y={y(t)} dy="0.32em" textAnchor="end" className="tnum fill-text-3 text-2xs">
                {money(t)}
              </text>
            </g>
          ))}

          {stopVisible && (
            <g>
              <line x1={M.left} x2={width - M.right} y1={y(stopAt)} y2={y(stopAt)} stroke="rgb(var(--warn))" strokeWidth={1.5} />
              <text x={width - M.right} y={y(stopAt) - 8} textAnchor="end" className="fill-text-2 text-xs font-semibold">
                Stops at {cents(stopAt)}
              </text>
            </g>
          )}

          <path d={area} fill="rgb(var(--text) / 0.08)" className="animate-fade" />
          <path d={line} fill="none" stroke="rgb(var(--text))" strokeWidth={2.25} strokeLinecap="round" strokeLinejoin="round" pathLength={1} className="draw" style={{ ["--len" as string]: 1 }} />

          {/* The forecast: from today to the reset at the cycle's pace so far. */}
          <line
            x1={lastX}
            y1={lastY}
            x2={x(totalDays)}
            y2={y(projectedEnd)}
            stroke="rgb(var(--text-2))"
            strokeWidth={1.75}
            strokeDasharray="5 6"
            className="animate-fade"
          />
          <circle cx={lastX} cy={lastY} r={5} fill="rgb(var(--text))" stroke="rgb(var(--panel))" strokeWidth={2} />
          <text x={x(totalDays)} y={y(projectedEnd) - 10} textAnchor="end" className="fill-text-2 text-xs font-semibold">
            {cents(projectedEnd)} by the reset
          </text>

          <line x1={M.left} x2={width - M.right} y1={y(0)} y2={y(0)} stroke="rgb(var(--hair-bright))" />
          <text x={M.left} y={height - 8} className="fill-text-3 text-2xs">{shortDate(grant.starts_at)}</text>
          <text x={width - M.right} y={height - 8} textAnchor="end" className="fill-text-3 text-2xs">
            Resets {shortDate(grant.expires_at)}
          </text>

          {cumulative.map((_, i) => (
            <rect
              key={i}
              x={x(i + 1) - step / 2}
              y={M.top}
              width={step}
              height={innerH}
              fill={hover === i ? "rgb(var(--text) / 0.04)" : "transparent"}
              onMouseEnter={() => setHover(i)}
            />
          ))}
        </svg>
      )}
      {active != null && (
        <div
          className="pointer-events-none absolute top-4 z-20 rounded-xl border border-hair-bright bg-overlay px-4 py-3 shadow-overlay animate-pop"
          style={x(active + 1) > width - 200 ? { right: width - x(active + 1) + 16 } : { left: x(active + 1) + 16 }}
        >
          <p className="text-sm font-semibold text-text-2">{shortDate(c.series[active].date)}</p>
          <p className="tnum mt-1 text-xl font-bold text-text">{cents(cumulative[active])}</p>
          <p className="text-sm text-text-3">spent by this day, {money(Number(c.series[active].credit_usd))} that day</p>
          <p className="text-sm text-text-3">{c.series[active].requests} requests</p>
        </div>
      )}
    </div>
  );
}
