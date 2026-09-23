import { Brain, Gauge, Gem, ImageIcon, Layers, type LucideIcon } from "lucide-react";
import type { PaidTier } from "@/lib/types";

/**
 * One hue per model type, defined once and reused everywhere a type is shown.
 *
 * The paid tiers run as a cost ladder — green, blue, amber, rose — so the
 * operator reads relative spend before reading a number. Image sits apart in
 * violet because it is a different kind of work, not a step on that ladder.
 */
export type AccentKey = "free" | "low" | "moderate" | "premium" | "image" | "brand";

export interface AccentDef {
  /** Scope class. Children then use accent-text / accent-tint / accent-bg. */
  scope: string;
  /** What the operator calls it. */
  label: string;
  /** The value to send as "model" in a chat request. */
  model?: string;
  icon: LucideIcon;
  /** One line explaining when to reach for it. */
  hint?: string;
}

export const ACCENTS: Record<AccentKey, AccentDef> = {
  free: {
    scope: "accent-free",
    label: "Free",
    model: "free",
    icon: Layers,
    hint: "Routed across free providers, ranked by measured health. Costs nothing.",
  },
  low: {
    scope: "accent-low",
    label: "Low",
    model: "paid:low",
    icon: Gauge,
    hint: "Cheapest paid models. This is what a plain paid request uses.",
  },
  moderate: {
    scope: "accent-moderate",
    label: "Moderate",
    model: "paid:moderate",
    icon: Brain,
    hint: "Balanced cost and capability, curated mid-range models first.",
  },
  premium: {
    scope: "accent-premium",
    label: "Premium",
    model: "paid:premium",
    icon: Gem,
    hint: "Highest capability, and the most expensive tier by a wide margin.",
  },
  image: {
    scope: "accent-image",
    label: "Image",
    model: "image",
    icon: ImageIcon,
    hint: "Cheapest image generation model.",
  },
  brand: {
    scope: "accent-brand",
    label: "Freeway",
    icon: Layers,
  },
};

export const PAID_TIER_ACCENT: Record<PaidTier, AccentKey> = {
  low: "low",
  moderate: "moderate",
  premium: "premium",
};

/** Resolves a usage log's model_type + model_tier to the right accent. */
export function accentForUsage(
  modelType: string | undefined | null,
  modelTier?: PaidTier | null
): AccentKey {
  if (modelTier && modelTier in PAID_TIER_ACCENT) {
    return PAID_TIER_ACCENT[modelTier];
  }
  switch ((modelType ?? "").toLowerCase()) {
    case "free":
      return "free";
    case "image":
      return "image";
    case "paid":
      return "low";
    default:
      return "brand";
  }
}

/** Label shown on a badge for a usage row. */
export function usageLabel(
  modelType: string | undefined | null,
  modelTier?: PaidTier | null
): string {
  if (modelTier) return `paid:${modelTier}`;
  return modelType || "unknown";
}

/**
 * Chart mark colour for a type. Validated for both light and dark against the
 * OKLCH lightness band, chroma floor, CVD separation and surface contrast, so
 * the same values are correct in either mode.
 */
export const MARK_COLORS: Record<AccentKey, string> = {
  free: "rgb(var(--mark-free))",
  low: "rgb(var(--mark-low))",
  moderate: "rgb(var(--mark-moderate))",
  premium: "rgb(var(--mark-premium))",
  image: "rgb(var(--mark-image))",
  brand: "rgb(var(--mark-brand))",
};
