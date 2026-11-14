"use client";

import { ReactNode, useLayoutEffect, useRef, useState } from "react";
import { cn } from "@/lib/utils/cn";

export interface Segment<T extends string> {
  value: T;
  label: ReactNode;
  /** Lane scope applied when this segment is the active one. */
  scope?: string;
}

/**
 * A single row of choices where exactly one is on. The thumb slides to the new
 * choice rather than jumping, so the change of scope is visible as it happens.
 */
export function Segmented<T extends string>({
  segments,
  value,
  onChange,
  label,
  size = "md",
  className,
}: {
  segments: Segment<T>[];
  value: T;
  onChange: (value: T) => void;
  label: string;
  size?: "sm" | "md";
  className?: string;
}) {
  const track = useRef<HTMLDivElement>(null);
  const [thumb, setThumb] = useState<{ left: number; width: number } | null>(null);
  const active = segments.find((s) => s.value === value);

  useLayoutEffect(() => {
    const el = track.current?.querySelector<HTMLButtonElement>(`[data-value="${CSS.escape(value)}"]`);
    if (!el) return;
    const measure = () => setThumb({ left: el.offsetLeft, width: el.offsetWidth });
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, [value, segments.length]);

  return (
    <div
      ref={track}
      role="tablist"
      aria-label={label}
      className={cn(
        "relative inline-flex items-stretch rounded-xl border border-hair bg-sunken p-1",
        className
      )}
    >
      {thumb && (
        <span
          aria-hidden
          className={cn(
            "absolute bottom-1 top-1 rounded-lg bg-raised shadow-card ring-1 ring-hair-bright",
            "transition-[left,width] duration-300 ease-out",
            active?.scope
          )}
          style={{ left: thumb.left, width: thumb.width }}
        >
          {active?.scope && (
            <span className="absolute inset-x-3 bottom-0.5 h-0.5 rounded-full lane-bg" />
          )}
        </span>
      )}
      {segments.map((s) => {
        const on = s.value === value;
        return (
          <button
            key={s.value}
            data-value={s.value}
            role="tab"
            aria-selected={on}
            onClick={() => onChange(s.value)}
            className={cn(
              "relative z-10 rounded-lg font-semibold transition-colors duration-200",
              size === "sm" ? "px-3 py-1 text-sm" : "px-4 py-1.5 text-base",
              on ? "text-text" : "text-text-3 hover:text-text-2"
            )}
          >
            {s.label}
          </button>
        );
      })}
    </div>
  );
}
