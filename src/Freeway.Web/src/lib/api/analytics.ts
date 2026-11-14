import { api } from "./client";
import type {
  GatewayAlert,
  GlobalSummary,
  LocalClaudeStatus,
  Overview,
  ProjectUsage,
  UsageLogsResponse,
} from "@/lib/types";

export const analyticsApi = {
  /**
   * Everything the dashboard needs in one call, including the daily series, who
   * answered, and the project → lane → backend flows. `projectId` limits every
   * figure to one project.
   */
  getOverview: (days = 30, projectId?: string) =>
    api.get<Overview>(
      `/admin/analytics/overview?days=${days}${projectId ? `&project_id=${projectId}` : ""}`
    ),

  // Global summary
  getGlobalSummary: () => api.get<GlobalSummary>("/admin/analytics/summary"),

  // Project usage
  getProjectUsage: (
    projectId: string,
    startDate?: string,
    endDate?: string
  ) => {
    const params = new URLSearchParams();
    params.set("project_id", projectId);
    if (startDate) params.set("start_date", startDate);
    if (endDate) params.set("end_date", endDate);
    return api.get<ProjectUsage>(`/admin/analytics/usage?${params.toString()}`);
  },

  // Usage logs
  getUsageLogs: (
    projectId: string,
    options?: {
      limit?: number;
      offset?: number;
      startDate?: string;
      endDate?: string;
    }
  ) => {
    const params = new URLSearchParams();
    params.set("project_id", projectId);
    if (options?.limit) params.set("limit", options.limit.toString());
    if (options?.offset) params.set("offset", options.offset.toString());
    if (options?.startDate) params.set("start_date", options.startDate);
    if (options?.endDate) params.set("end_date", options.endDate);
    return api.get<UsageLogsResponse>(
      `/admin/analytics/logs?${params.toString()}`
    );
  },

  /** The Claude Code bridge's last known state and the Claude plan's usage. Makes no call upstream. */
  getLocalClaude: () => api.get<LocalClaudeStatus>("/admin/local-claude"),

  /** Probes the bridge now instead of waiting for the hourly check. */
  checkLocalClaude: () => api.post<LocalClaudeStatus>("/admin/local-claude/check"),

  /** Alerts firing right now (spend, credit, keys). Runs the checks, so call it sparingly. */
  getAlerts: () => api.get<{ count: number; alerts: GatewayAlert[] }>("/admin/notifications/alerts"),
};
