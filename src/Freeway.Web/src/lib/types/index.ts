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
  response_content?: string;
  finish_reason?: string;
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
