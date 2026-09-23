import { HTMLAttributes } from "react";
import { cn } from "@/lib/utils/cn";

interface TagProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: "neutral" | "lane" | "ok" | "warn" | "bad" | "accent";
  dot?: boolean;
  mono?: boolean;
}

/** Small status carrier. Colour here always means something. */
export function Tag({ tone = "neutral", dot, mono, className, children, ...props }: TagProps) {
  const tones = {
    neutral: "bg-sunken text-text-2 border-hair",
    lane: "lane-soft lane-fg lane-edge",
    ok: "bg-ok/12 text-ok border-ok/25",
    warn: "bg-warn/12 text-warn border-warn/25",
    bad: "bg-bad/12 text-bad border-bad/25",
    accent: "bg-accent/12 text-accent border-accent/25",
  };

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded border px-1.5 py-0.5 text-2xs font-medium",
        tones[tone],
        mono && "font-mono tracking-normal",
        className
      )}
      {...props}
    >
      {dot && <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden />}
      {children}
    </span>
  );
}
