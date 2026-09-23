/** Compact magnitude for a figure that sits next to other figures. */
export function compact(n: number | undefined | null): string {
  if (n == null) return "0";
  const abs = Math.abs(n);
  if (abs >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}B`;
  if (abs >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (abs >= 10_000) return `${(n / 1_000).toFixed(0)}K`;
  if (abs >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return Math.round(n).toLocaleString("en-US");
}

/** Exact count with separators, for a figure being read rather than compared. */
export function count(n: number | undefined | null): string {
  return (n ?? 0).toLocaleString("en-US");
}

/**
 * Spend can span six orders of magnitude between lanes, so the precision follows the
 * value: a real charge of a few millionths must not print as "$0.00".
 */
export function money(n: number | undefined | null): string {
  if (n == null || n === 0) return "$0.00";
  const abs = Math.abs(n);
  if (abs < 0.000001) return "<$0.000001";
  if (abs < 0.01) return `$${n.toFixed(6)}`;
  if (abs < 1) return `$${n.toFixed(4)}`;
  if (abs < 1000) return `$${n.toFixed(2)}`;
  return `$${compact(n)}`;
}

/** Per-million-token price, the unit model catalogs quote. */
export function perMillion(pricePerToken: string | number | undefined | null): string {
  const v = typeof pricePerToken === "string" ? parseFloat(pricePerToken) : pricePerToken;
  if (v == null || Number.isNaN(v)) return "—";
  if (v === 0) return "free";
  return money(v * 1_000_000);
}

export function pct(n: number | undefined | null, digits = 0): string {
  if (n == null) return "0%";
  return `${n.toFixed(digits)}%`;
}

export function ms(n: number | undefined | null): string {
  if (n == null) return "—";
  if (n >= 1000) return `${(n / 1000).toFixed(1)}s`;
  return `${Math.round(n)}ms`;
}

export function shortDate(date: string | Date): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return d.toLocaleDateString("en-US", { month: "short", day: "numeric" });
}

export function fullDate(date: string | Date): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return d.toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
}

export function dateTime(date: string | Date): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return d.toLocaleString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function ago(date: string | Date): string {
  const d = typeof date === "string" ? new Date(date) : date;
  const seconds = Math.floor((Date.now() - d.getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;
  return shortDate(d);
}

/** Splits "vendor/model-name" so the vendor can be set back typographically. */
export function splitModelId(modelId: string | undefined | null): {
  vendor: string | null;
  name: string;
} {
  if (!modelId) return { vendor: null, name: "unknown" };
  const i = modelId.indexOf("/");
  if (i === -1) return { vendor: null, name: modelId };
  return { vendor: modelId.slice(0, i), name: modelId.slice(i + 1) };
}

export function modelShortName(modelId: string | undefined | null): string {
  return splitModelId(modelId).name;
}

/** Change between two values as a signed percentage. Null when there is no baseline. */
export function change(current: number, previous: number): number | null {
  if (!previous) return null;
  return ((current - previous) / previous) * 100;
}

export function startOfMonthIso(): string {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1)).toISOString();
}

export function daysAgoIso(days: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - days);
  return d.toISOString();
}

export function currentMonthLabel(): string {
  return new Date().toLocaleDateString("en-US", { month: "long", year: "numeric" });
}
