import { api } from "./client";
import type { GlobalSummary, ProjectUsage, UsageLogsResponse } from "@/lib/types";

export const analyticsApi = {
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
};
