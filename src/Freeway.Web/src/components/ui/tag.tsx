import { HTMLAttributes } from "react";
import { cn } from "@/lib/utils/cn";

interface TagProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: "neutral" | "lane" | "ok" | "warn" | "bad" | "accent";
  dot?: boolean;
  mono?: boolean;
}

/**
 * Small status carrier. The text is always ink; colour sits in the dot and the wash,
 * so a tag is readable whatever it is tinted with.
 */
export function Tag({ tone = "neutral", dot, mono, className, children, ...props }: TagProps) {
  const tones = {
    neutral: "bg-sunken text-text-2 border-hair",
    lane: "lane-soft text-text lane-edge",
    ok: "bg-ok/12 text-text border-ok/30",
    warn: "bg-warn/12 text-text border-warn/35",
    bad: "bg-bad/12 text-text border-bad/35",
    accent: "bg-text/[0.07] text-text border-hair-bright",
  };
  const dots = {
    neutral: "bg-text-3",
    lane: "lane-bg",
    ok: "bg-ok",
    warn: "bg-warn",
    bad: "bg-bad",
    accent: "bg-text",
  };

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2.5 py-0.5 text-xs font-semibold",
        tones[tone],
        mono && "font-mono font-medium",
        className
      )}
      {...props}
    >
      {dot && <span className={cn("h-1.5 w-1.5 shrink-0 rounded-full", dots[tone])} aria-hidden />}
      {children}
    </span>
  );
}
