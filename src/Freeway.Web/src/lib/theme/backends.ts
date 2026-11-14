import { CircleHelp, Coins, CreditCard, Gift, SquareTerminal, type LucideIcon } from "lucide-react";
import type { BackendKey } from "@/lib/types";

/**
 * Who answered a request. A second vocabulary beside the lanes: the lane is what a
 * caller asked for, the backend is what actually served it.
 *
 * Backends deliberately carry no colour of their own -- every hue in the console
 * already means a lane -- so they are told apart by name and icon, and the one that
 * costs money says so in words.
 */
export interface Backend {
  key: BackendKey;
  label: string;
  icon: LucideIcon;
  /** Charged per request. Only OpenRouter is. */
  billed: boolean;
  blurb: string;
}

export const BACKENDS: Record<BackendKey, Backend> = {
  credit: {
    key: "credit",
    label: "Claude credit",
    icon: Coins,
    billed: false,
    blurb: "The Max plan's monthly API credit. Prepaid, and it expires each cycle.",
  },
  bridge: {
    key: "bridge",
    label: "Claude Code",
    icon: SquareTerminal,
    billed: false,
    blurb: "The Max subscription on the server, within the plan's limits.",
  },
  free: {
    key: "free",
    label: "Free providers",
    icon: Gift,
    billed: false,
    blurb: "Groq, Gemini, Mistral and OpenRouter's free models.",
  },
  openrouter: {
    key: "openrouter",
    label: "OpenRouter",
    icon: CreditCard,
    billed: true,
    blurb: "Paid models, billed per request. The last resort for every paid lane.",
  },
  other: {
    key: "other",
    label: "Other",
    icon: CircleHelp,
    billed: false,
    blurb: "Rows with no recorded provider.",
  },
};

/** In the order a paid request tries them, with free providers between. */
export const BACKEND_ORDER: BackendKey[] = ["credit", "bridge", "free", "openrouter", "other"];

export function backendOf(key?: string | null): Backend {
  return key && key in BACKENDS ? BACKENDS[key as BackendKey] : BACKENDS.other;
}
