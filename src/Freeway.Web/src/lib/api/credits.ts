import { api } from "./client";
import type { CreditGrant, CreditOverview } from "@/lib/types";

export const creditsApi = {
  /** The current cycle, recounted from the usage logs on every call. */
  getOverview: () => api.get<CreditOverview>("/admin/credits"),

  /** Checks the key with a free call and lifts a pause left by an earlier rejection. */
  check: () =>
    api.post<{ configured: boolean; can_serve: boolean; state: string; summary: string; last_error?: string | null }>(
      "/admin/credits/check"
    ),

  updateGrant: (
    id: string,
    changes: { amount_usd?: number; starts_at?: string; expires_at?: string; note?: string }
  ) => api.put<CreditGrant>(`/admin/credits/grants/${id}`, changes),

  /** When the API key expires, as the Console shows it. Null for a key that never does. */
  setKeyExpiry: (expiresAt: string | null) =>
    api.put<{ hint: string; expires_at: string | null; expiry_source: string }>("/admin/credits/key", {
      expires_at: expiresAt,
    }),

  sendTestReminder: () =>
    api.post<{ sent: boolean; recipient?: string | null; detail: string }>("/admin/credits/key/test-reminder"),
};
