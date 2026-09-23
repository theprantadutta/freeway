"use client";

import { ReactNode } from "react";
import { cn } from "@/lib/utils/cn";

export interface Segment<T extends string> {
  value: T;
  label: ReactNode;
  /** Lane scope applied when this segment is the active one. */
  scope?: string;
}

/**
 * A single row of choices where exactly one is on. Replaces the old tab component:
 * the active segment is marked by a filled underline rather than a raised pill, so
 * it reads as a setting rather than as a stack of panels.
 */
export function Segmented<T extends string>({
  segments,
  value,
  onChange,
  label,
  className,
}: {
  segments: Segment<T>[];
  value: T;
  onChange: (value: T) => void;
  label: string;
  className?: string;
}) {
  return (
    <div
      role="tablist"
      aria-label={label}
      className={cn("inline-flex items-stretch gap-0.5 rounded bg-sunken p-0.5", className)}
    >
      {segments.map((s) => {
        const active = s.value === value;
        return (
          <button
            key={s.value}
            role="tab"
            aria-selected={active}
            onClick={() => onChange(s.value)}
            className={cn(
              "relative rounded-[0.3125rem] px-3 py-1.5 text-sm font-medium",
              "transition-[color,background-color] duration-150 ease-out",
              active
                ? cn("bg-raised text-text", s.scope)
                : "text-text-3 hover:text-text-2"
            )}
          >
            {active && s.scope && (
              <span
                className="absolute inset-x-2 -bottom-px h-0.5 rounded-full lane-bg"
                aria-hidden
              />
            )}
            {s.label}
          </button>
        );
      })}
    </div>
  );
}
