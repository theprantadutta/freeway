// Auth types
export interface User {
  id: string;
  email: string;
  name?: string;
  is_admin: boolean;
  created_at: string;
  is_active: boolean;
  last_login_at?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  user: User;
  expires_at: string;
}

// Project types
export interface Project {
  id: string;
  name: string;
  api_key_prefix: string;
  created_at: string;
  updated_at: string;
  is_active: boolean;
  rate_limit_per_minute: number;
  metadata?: Record<string, unknown>;
}

export interface ProjectWithKey extends Project {
  api_key: string;
}

export interface CreateProjectRequest {
  name: string;
  rate_limit_per_minute?: number;
  metadata?: Record<string, unknown>;
}

export interface UpdateProjectRequest {
  name?: string;
  is_active?: boolean;
  rate_limit_per_minute?: number;
  metadata?: Record<string, unknown>;
}

export interface RotateKeyResult {
  id: string;
  api_key: string;
  api_key_prefix: string;
}

// Model types

/** Cost/capability tier for the "paid" virtual model. */
export type PaidTier = "low" | "moderate" | "premium";

export const PAID_TIERS: PaidTier[] = ["low", "moderate", "premium"];

export interface ModelInfo {
  model_id: string;
  model_name: string;
  description?: string;
  context_length: number;
  pricing: {
    prompt: string;
    completion: string;
  };
  rank?: number;
  tier?: PaidTier;
  is_curated?: boolean;
}

export interface SelectedModel {
  model_id: string;
  model_name?: string;
  context_length?: number;
  tier?: PaidTier;
  is_curated?: boolean;
  pricing?: {
    prompt: string;
    completion: string;
  };
}

// Analytics types
export interface GlobalSummary {
  total_projects: number;
  active_projects: number;
  requests_today: number;
  requests_this_month: number;
  total_cost_today: number;
  total_cost_this_month: number;
}

export interface UsageSummary {
  total_requests: number;
  success_rate: number;
  total_tokens: number;
  total_input_tokens: number;
  total_output_tokens: number;
  total_cost_usd: number;
  avg_response_time_ms: number;
  failed_requests: number;
}

export interface ModelUsageStats {
  model_id: string;
  model_type: string;
  model_tier?: PaidTier | null;
  requests: number;
  tokens: number;
  cost_usd: number;
}

export interface ProjectUsage {
  project_id: string;
  summary: UsageSummary;
  by_model: ModelUsageStats[];
}

export interface UsageLog {
  id: string;
  project_id: string;
  model_id: string;
  model_type: string;
  model_tier?: PaidTier | null;
  input_tokens: number;
  output_tokens: number;
  total_tokens: number;
  response_time_ms: number;
  cost_usd: number;
  success: boolean;
  error_message?: string;
  request_id?: string;
  provider?: string;
  /** "provider", "subscription", "credit", "free_tier", "estimated"… */
  cost_source?: string | null;
  /** Taken off the Claude API credit rather than billed; cost_usd is 0 on these rows. */
  credit_cost_usd?: number | null;
  /** What a paid model would have charged, for rows served by a subscription or credit. */
  avoided_cost_usd?: number | null;
  response_content?: string;
  finish_reason?: string;
  /** The caller asked for a stream. */
  streamed?: boolean;
  /** For a stream, the wait before its first token. */
  time_to_first_token_ms?: number | null;
  created_at: string;
}

export interface UsageLogsResponse {
  logs: UsageLog[];
  total_count: number;
  limit: number;
  offset: number;
}

// Provider types
export interface ProviderInfo {
  name: string;
  is_enabled: boolean;
  model_count?: number;
}

// Dashboard overview — one call, everything the console board needs.
export interface UsagePoint {
  date: string;
  requests: number;
  failures: number;
  cost: number;
  tokens: number;
}

export interface LaneUsage {
  lane: string;
  requests: number;
  cost: number;
  tokens: number;
}

export interface TopModel {
  model_id: string;
  model_type: string;
  model_tier?: PaidTier | null;
  requests: number;
  cost: number;
  tokens: number;
}

export interface TopProject {
  project_id: string;
  name: string;
  requests: number;
  cost: number;
  is_active: boolean;
}

export interface RecentRequest {
  id: string;
  project_name: string;
  model_id: string;
  model_type: string;
  model_tier?: PaidTier | null;
  success: boolean;
  response_time_ms: number;
  cost_usd: number;
  total_tokens: number;
  created_at: string;
}

export interface OverviewTotals {
  total_projects: number;
  active_projects: number;
  requests_today: number;
  requests_this_month: number;
  requests_all_time: number;
  cost_today: number;
  cost_this_month: number;
  cost_all_time: number;
  cost_previous_month: number;
  requests_previous_month: number;
  tokens_this_month: number;
  failures_this_month: number;
  success_rate_this_month: number;
  avg_response_ms_this_month: number;
}

export interface Overview {
  totals: OverviewTotals;
  series: UsagePoint[];
  lanes: LaneUsage[];
  top_models: TopModel[];
  top_projects: TopProject[];
  recent: RecentRequest[];
  days: number;
}

// Claude API credit — the plan's monthly allowance, spent before anything is billed.
export type CreditState = "off" | "unchecked" | "active" | "no_grant" | "exhausted" | "blocked";

export interface CreditGrant {
  id: string;
  amount_usd: number;
  starts_at: string;
  expires_at: string;
  source: "auto" | "manual";
  note?: string | null;
  used_usd: number;
  requests: number;
}

export interface CreditKey {
  hint: string;
  fingerprint: string;
  first_seen_at: string;
  last_seen_at: string;
  expires_at?: string | null;
  expiry_source: "estimated" | "manual";
  days_left?: number | null;
  last_rejected_at?: string | null;
  last_rejection?: string | null;
  warned_at?: string | null;
  expired_notice_at?: string | null;
}

export interface CreditBreakdown {
  key: string;
  name: string;
  tier?: PaidTier | null;
  requests: number;
  credit_usd: number;
  tokens: number;
}

export interface CreditRequest {
  id: string;
  created_at: string;
  project_name: string;
  model_id: string;
  model_tier?: PaidTier | null;
  input_tokens: number;
  output_tokens: number;
  credit_usd: number;
  avoided_usd?: number | null;
  response_time_ms: number;
  streamed: boolean;
  success: boolean;
}

export interface CreditOverview {
  provider: string;
  configured: boolean;
  can_serve: boolean;
  state: CreditState;
  summary: string;
  blocked_reason?: string | null;
  blocked_until?: string | null;
  last_error?: string | null;
  last_error_at?: string | null;
  refreshed_at?: string | null;
  last_used_at?: string | null;
  lanes: { tier: PaidTier; model: string }[];
  stop_at_percent: number;
  grant?: CreditGrant | null;
  key?: CreditKey | null;
  used_usd: number;
  remaining_usd: number;
  usable_usd: number;
  used_percent: number;
  days_total: number;
  days_elapsed: number;
  days_left: number;
  burn_per_day_usd: number;
  projected_usd: number;
  projected_unused_usd: number;
  requests: number;
  failed_requests: number;
  input_tokens: number;
  output_tokens: number;
  avoided_usd: number;
  series: { date: string; credit_usd: number; requests: number; tokens: number }[];
  by_project: CreditBreakdown[];
  by_model: CreditBreakdown[];
  recent: CreditRequest[];
  history: CreditGrant[];
}
