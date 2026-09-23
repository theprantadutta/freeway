import { Gauge, Gem, ImageIcon, Layers, Sparkles, type LucideIcon } from "lucide-react";
import type { PaidTier } from "@/lib/types";

/**
 * One hue per lane, defined once and reused wherever a lane is shown.
 *
 * The paid tiers climb green → blue → amber → rose as a cost ladder, so relative
 * spend is legible before any number is read. Image sits apart in violet: it is a
 * different kind of work, not a step on that ladder.
 */
export type LaneKey = "free" | "low" | "moderate" | "premium" | "image" | "other";

export interface Lane {
  key: LaneKey;
  /** Scope class. Children then use lane-fg / lane-bg / lane-soft / lane-edge. */
  scope: string;
  label: string;
  /** What to send as "model" in a chat request. */
  model?: string;
  icon: LucideIcon;
  blurb?: string;
}

export const LANES: Record<LaneKey, Lane> = {
  free: {
    key: "free",
    scope: "lane-free",
    label: "Free",
    model: "free",
    icon: Layers,
    blurb: "Across free providers, ranked by measured health. Never billed.",
  },
  low: {
    key: "low",
    scope: "lane-low",
    label: "Low",
    model: "paid:low",
    icon: Gauge,
    blurb: "Cheapest paid models. What a plain paid request uses.",
  },
  moderate: {
    key: "moderate",
    scope: "lane-moderate",
    label: "Moderate",
    model: "paid:moderate",
    icon: Sparkles,
    blurb: "Balanced cost and capability, curated mid-range first.",
  },
  premium: {
    key: "premium",
    scope: "lane-premium",
    label: "Premium",
    model: "paid:premium",
    icon: Gem,
    blurb: "Highest capability, and by a wide margin the most expensive.",
  },
  image: {
    key: "image",
    scope: "lane-image",
    label: "Image",
    model: "image",
    icon: ImageIcon,
    blurb: "Cheapest image generation, ranked by what it costs to generate.",
  },
  other: {
    key: "other",
    scope: "lane-other",
    label: "Other",
    icon: Layers,
  },
};

/** The lanes in cost order, for legends and pickers. */
export const LANE_ORDER: LaneKey[] = ["free", "low", "moderate", "premium", "image"];

export const TIER_LANE: Record<PaidTier, LaneKey> = {
  low: "low",
  moderate: "moderate",
  premium: "premium",
};

/** Resolves a usage row's model_type + model_tier onto a lane. */
export function laneOf(
  modelType?: string | null,
  modelTier?: PaidTier | string | null
): Lane {
  if (modelTier && modelTier in LANES) return LANES[modelTier as LaneKey];

  switch ((modelType ?? "").toLowerCase()) {
    case "free":
      return LANES.free;
    case "image":
      return LANES.image;
    case "paid":
      return LANES.low;
    default:
      return LANES.other;
  }
}

/** Label for a usage row, e.g. "paid:premium". */
export function laneLabel(
  modelType?: string | null,
  modelTier?: PaidTier | string | null
): string {
  if (modelTier) return `paid:${modelTier}`;
  return modelType || "unknown";
}
