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
export interface ModelInfo {
  id: string;
  name: string;
  description?: string;
  context_length: number;
  pricing: {
    prompt: number;
    completion: number;
  };
  top_provider?: {
    max_completion_tokens?: number;
  };
}

export interface SelectedModel {
  model_id: string;
  model_name?: string;
  context_length?: number;
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
