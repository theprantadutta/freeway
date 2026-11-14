"use client";

import { ReactNode, useEffect, useId, useState } from "react";
import { cn } from "@/lib/utils/cn";
import { monotonePath, type Pt } from "@/lib/charts";
import { useCountUp } from "@/lib/hooks/motion";

/**
 * A number that counts to its value. Big standalone figures use proportional digits
 * (`num`); the formatter decides precision, so $0.000259 and 1.1K both land right.
 */
export function AnimatedNumber({
  value,
  format,
  className,
}: {
  value: number;
  format: (v: number) => string;
  className?: string;
}) {
  const v = useCountUp(value);
  return (
    <span className={cn("num", className)}>
      {format(v)}
      <span className="sr-only">{format(value)}</span>
    </span>
  );
}

/**
 * Twelve-to-ninety points of trend in the de-emphasis ink, the last point marked.
 * Draws itself in once. No axes: a sparkline says "rising" or "quiet", the tile
 * beside it says how much.
 */
export function Sparkline({
  values,
  height = 40,
  className,
  emphasis = false,
}: {
  values: number[];
  height?: number;
  className?: string;
  /** Ink the line at full strength instead of the quiet de-emphasis tone. */
  emphasis?: boolean;
}) {
  const id = useId().replace(/:/g, "");
  const width = 160;
  if (values.length < 2) return <div style={{ height }} className={className} aria-hidden />;

  const max = Math.max(...values, 0);
  const pad = 3;
  const pts: Pt[] = values.map((v, i) => [
    (i / (values.length - 1)) * width,
    max === 0 ? height - pad : height - pad - (v / max) * (height - pad * 2),
  ]);
  const line = monotonePath(pts);
  const area = `${line}L${width},${height}L0,${height}Z`;

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      preserveAspectRatio="none"
      className={cn("w-full overflow-visible", emphasis ? "text-text" : "text-text-3", className)}
      style={{ height }}
      aria-hidden
    >
      <defs>
        <linearGradient id={`sg-${id}`} x1="0" x2="0" y1="0" y2="1">
          <stop offset="0%" stopColor="currentColor" stopOpacity="0.22" />
          <stop offset="100%" stopColor="currentColor" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={area} fill={`url(#sg-${id})`} className="animate-fade" />
      <path
        d={line}
        fill="none"
        stroke="currentColor"
        strokeWidth={1.75}
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
        pathLength={1}
        className="draw"
        style={{ ["--len" as string]: 1 }}
      />
    </svg>
  );
}

/**
 * A ratio against a limit, as a ring. The fill carries severity: ink while there is
 * room, warning past `warnAt`, critical past `badAt`. It sweeps to its value once.
 */
export function Ring({
  value,
  size = 112,
  stroke = 10,
  warnAt = 80,
  badAt = 95,
  children,
  label,
}: {
  /** 0–100. */
  value: number;
  size?: number;
  stroke?: number;
  warnAt?: number;
  badAt?: number;
  children?: ReactNode;
  label: string;
}) {
  const [shown, setShown] = useState(0);
  useEffect(() => {
    const t = requestAnimationFrame(() => setShown(Math.max(0, Math.min(100, value))));
    return () => cancelAnimationFrame(t);
  }, [value]);

  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const tone = value >= badAt ? "text-bad" : value >= warnAt ? "text-warn" : "text-text";

  return (
    <div className="relative shrink-0" style={{ width: size, height: size }} role="img" aria-label={label}>
      <svg width={size} height={size} className="-rotate-90">
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="rgb(var(--hair-bright))" strokeWidth={stroke} />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          stroke="currentColor"
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={c}
          strokeDashoffset={c * (1 - shown / 100)}
          className={cn(tone, "transition-[stroke-dashoffset] duration-[1400ms] ease-out")}
        />
      </svg>
      <div className="absolute inset-0 grid place-items-center text-center">{children}</div>
    </div>
  );
}

/** A ratio against a limit, as a bar. Same severity rules as the ring. */
export function Meter({
  value,
  warnAt = 80,
  badAt = 95,
  marker,
  className,
  label,
}: {
  /** 0–100. */
  value: number;
  warnAt?: number;
  badAt?: number;
  /** Where a limit sits on the bar, 0–100, e.g. the point the credit stops being used. */
  marker?: number;
  className?: string;
  label: string;
}) {
  const [shown, setShown] = useState(0);
  useEffect(() => {
    const t = requestAnimationFrame(() => setShown(Math.max(0, Math.min(100, value))));
    return () => cancelAnimationFrame(t);
  }, [value]);
  const tone = value >= badAt ? "bg-bad" : value >= warnAt ? "bg-warn" : "bg-text";

  return (
    <div
      className={cn("relative h-2 w-full rounded-full bg-hair-bright/70", className)}
      role="meter"
      aria-label={label}
      aria-valuenow={Math.round(value)}
      aria-valuemin={0}
      aria-valuemax={100}
    >
      <div
        className={cn("h-full rounded-full transition-[width] duration-[1200ms] ease-out", tone)}
        style={{ width: `${shown > 0 ? Math.max(shown, 1.5) : 0}%` }}
      />
      {marker != null && (
        <span className="absolute -top-1 h-4 w-0.5 rounded-full bg-text-2" style={{ left: `${marker}%` }} aria-hidden />
      )}
    </div>
  );
}

/** Signed change against a named period. Down is good for cost, up is good for traffic. */
export function Delta({
  current,
  previous,
  upIsGood = true,
  suffix = "vs previous period",
  className,
}: {
  current: number;
  previous: number;
  upIsGood?: boolean;
  suffix?: string;
  className?: string;
}) {
  if (!previous) {
    return <p className={cn("text-sm text-text-3", className)}>No earlier period to compare</p>;
  }
  const change = ((current - previous) / previous) * 100;
  if (Math.abs(change) < 0.5) {
    return <p className={cn("text-sm text-text-3", className)}>Flat {suffix}</p>;
  }
  const up = change > 0;
  const good = up === upIsGood;
  const label = Math.abs(change) >= 1000 ? `×${(current / previous).toFixed(0)}` : `${Math.abs(change).toFixed(0)}%`;

  return (
    <p className={cn("flex items-center gap-1.5 text-sm", className)}>
      <span className={cn("inline-flex items-center gap-1 font-bold", good ? "text-ok" : "text-bad")}>
        <svg viewBox="0 0 10 10" className={cn("h-2.5 w-2.5", !up && "rotate-180")} aria-hidden>
          <path d="M5 1 9 8H1z" fill="currentColor" />
        </svg>
        {label}
      </span>
      <span className="text-text-3">{suffix}</span>
    </p>
  );
}

/** The dot that marks a lane, beside ink text. */
export function LaneDot({ scope, className }: { scope: string; className?: string }) {
  return <span className={cn(scope, "inline-block h-2.5 w-2.5 shrink-0 rounded-full lane-bg", className)} aria-hidden />;
}
