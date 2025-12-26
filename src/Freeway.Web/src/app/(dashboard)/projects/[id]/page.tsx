"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeft,
  FolderKanban,
  Key,
  Gauge,
  Calendar,
  Activity,
  Zap,
  DollarSign,
  Clock,
  AlertCircle,
  ChevronDown,
  ChevronUp,
  Copy,
  Check,
} from "lucide-react";
import Link from "next/link";
import {
  PieChart,
  Pie,
  Cell,
  ResponsiveContainer,
  Tooltip,
} from "recharts";
import { Header } from "@/components/layout/header";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { StatCard } from "@/components/ui/stat-card";
import { Skeleton, SkeletonCard } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { projectsApi } from "@/lib/api/projects";
import { analyticsApi } from "@/lib/api/analytics";
import {
  formatNumber,
  formatCurrency,
  formatDateTime,
  formatRelativeTime,
  getModelShortName,
} from "@/lib/utils/format";
import type { UsageLog } from "@/lib/types";

const CHART_COLORS = [
  "#0ea5e9",
  "#8b5cf6",
  "#10b981",
  "#f59e0b",
  "#ef4444",
  "#ec4899",
  "#6366f1",
  "#14b8a6",
];

export default function ProjectDetailsPage() {
  const params = useParams();
  const projectId = params.id as string;

  const [logsLimit, setLogsLimit] = useState(20);

  const { data: project, isLoading: projectLoading } = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => projectsApi.getProject(projectId),
  });

  const { data: usage, isLoading: usageLoading } = useQuery({
    queryKey: ["project-usage", projectId],
    queryFn: () => analyticsApi.getProjectUsage(projectId),
  });

  const { data: logsData, isLoading: logsLoading } = useQuery({
    queryKey: ["project-logs", projectId, logsLimit],
    queryFn: () =>
      analyticsApi.getUsageLogs(projectId, { limit: logsLimit, offset: 0 }),
  });

  if (projectLoading) {
    return (
      <div className="flex flex-col h-full">
        <Header title="Loading..." />
        <div className="p-6 space-y-6">
          <SkeletonCard />
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
            {[...Array(8)].map((_, i) => (
              <Skeleton key={i} className="h-24" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (!project) {
    return (
      <div className="flex flex-col h-full">
        <Header title="Project Not Found" />
        <div className="flex-1 flex items-center justify-center">
          <EmptyState
            icon={FolderKanban}
            title="Project not found"
            description="The project you're looking for doesn't exist."
            action={
              <Link href="/projects">
                <Button variant="outline">
                  <ArrowLeft className="h-4 w-4 mr-2" />
                  Back to Projects
                </Button>
              </Link>
            }
          />
        </div>
      </div>
    );
  }

  const summary = usage?.summary;
  const byModel = usage?.by_model || [];
  const logs = logsData?.logs || [];
  const totalLogs = logsData?.total_count || 0;

  return (
    <div className="flex flex-col h-full">
      <Header title={project.name} subtitle="Project details and analytics" />

      <div className="flex-1 p-4 md:p-6 space-y-6 overflow-y-auto">
        {/* Back Link */}
        <Link
          href="/projects"
          className="inline-flex items-center gap-2 text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Projects
        </Link>

        {/* Project Info */}
        <Card>
          <CardContent className="py-6">
            <div className="flex items-start gap-4">
              <div className="p-3 bg-primary-100 dark:bg-primary-900/30 rounded-lg">
                <FolderKanban className="h-6 w-6 text-primary-600 dark:text-primary-400" />
              </div>
              <div className="flex-1">
                <div className="flex items-center gap-2 mb-2">
                  <h2 className="text-xl font-semibold text-gray-900 dark:text-gray-100">
                    {project.name}
                  </h2>
                  <Badge variant={project.is_active ? "success" : "default"}>
                    {project.is_active ? "Active" : "Inactive"}
                  </Badge>
                </div>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                  <div>
                    <p className="text-gray-500 dark:text-gray-400">
                      Project ID
                    </p>
                    <p className="font-mono text-gray-900 dark:text-gray-100 text-xs mt-1 break-all">
                      {project.id}
                    </p>
                  </div>
                  <div>
                    <p className="text-gray-500 dark:text-gray-400">
                      API Key Prefix
                    </p>
                    <p className="font-mono text-gray-900 dark:text-gray-100 mt-1">
                      {project.api_key_prefix}...
                    </p>
                  </div>
                  <div>
                    <p className="text-gray-500 dark:text-gray-400">
                      Rate Limit
                    </p>
                    <p className="text-gray-900 dark:text-gray-100 mt-1">
                      {project.rate_limit_per_minute} req/min
                    </p>
                  </div>
                  <div>
                    <p className="text-gray-500 dark:text-gray-400">Created</p>
                    <p className="text-gray-900 dark:text-gray-100 mt-1">
                      {formatDateTime(project.created_at)}
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Usage Stats */}
        <section>
          <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
            Usage Statistics
          </h3>
          {usageLoading ? (
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
              {[...Array(8)].map((_, i) => (
                <Skeleton key={i} className="h-24" />
              ))}
            </div>
          ) : summary ? (
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
              <StatCard
                title="Total Requests"
                value={formatNumber(summary.total_requests)}
                icon={Zap}
                iconColor="text-blue-500"
              />
              <StatCard
                title="Success Rate"
                value={`${(summary.success_rate ?? 0).toFixed(1)}%`}
                icon={Activity}
                iconColor="text-green-500"
              />
              <StatCard
                title="Total Tokens"
                value={formatNumber(summary.total_tokens)}
                icon={Key}
                iconColor="text-amber-500"
              />
              <StatCard
                title="Total Cost"
                value={formatCurrency(summary.total_cost_usd, 6)}
                icon={DollarSign}
                iconColor="text-purple-500"
              />
              <StatCard
                title="Input Tokens"
                value={formatNumber(summary.total_input_tokens)}
                icon={Gauge}
                iconColor="text-blue-500"
              />
              <StatCard
                title="Output Tokens"
                value={formatNumber(summary.total_output_tokens)}
                icon={Gauge}
                iconColor="text-indigo-500"
              />
              <StatCard
                title="Avg Response"
                value={`${(summary.avg_response_time_ms ?? 0).toFixed(0)}ms`}
                icon={Clock}
                iconColor="text-amber-500"
              />
              <StatCard
                title="Failed Requests"
                value={formatNumber(summary.failed_requests)}
                icon={AlertCircle}
                iconColor="text-red-500"
              />
            </div>
          ) : (
            <EmptyState
              icon={Activity}
              title="No usage data"
              description="Usage statistics will appear here once this project receives traffic."
            />
          )}
        </section>

        {/* Usage by Model */}
        {byModel.length > 0 && (
          <section>
            <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
              Usage by Model
            </h3>
            <div className="grid lg:grid-cols-2 gap-4">
              {/* Pie Chart */}
              <Card>
                <CardContent className="py-6">
                  <div className="h-64">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie
                          data={byModel}
                          dataKey="requests"
                          nameKey="model_id"
                          cx="50%"
                          cy="50%"
                          outerRadius={80}
                          label={({ percent }) =>
                            percent > 0.05
                              ? `${(percent * 100).toFixed(0)}%`
                              : ""
                          }
                        >
                          {byModel.map((_, index) => (
                            <Cell
                              key={`cell-${index}`}
                              fill={CHART_COLORS[index % CHART_COLORS.length]}
                            />
                          ))}
                        </Pie>
                        <Tooltip
                          formatter={(value: number) => [value, "Requests"]}
                          labelFormatter={(label) => getModelShortName(label)}
                        />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                  <div className="mt-4 space-y-2">
                    {byModel.slice(0, 5).map((model, index) => (
                      <div
                        key={model.model_id}
                        className="flex items-center gap-2"
                      >
                        <div
                          className="w-3 h-3 rounded"
                          style={{
                            backgroundColor:
                              CHART_COLORS[index % CHART_COLORS.length],
                          }}
                        />
                        <span className="flex-1 text-sm text-gray-600 dark:text-gray-400 truncate">
                          {getModelShortName(model.model_id)}
                        </span>
                        <span className="text-sm font-medium text-gray-900 dark:text-gray-100">
                          {formatNumber(model.requests)}
                        </span>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>

              {/* Table */}
              <Card>
                <CardContent className="py-0">
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-gray-200 dark:border-gray-800">
                          <th className="py-3 px-2 text-left font-medium text-gray-500 dark:text-gray-400">
                            Model
                          </th>
                          <th className="py-3 px-2 text-left font-medium text-gray-500 dark:text-gray-400">
                            Type
                          </th>
                          <th className="py-3 px-2 text-right font-medium text-gray-500 dark:text-gray-400">
                            Requests
                          </th>
                          <th className="py-3 px-2 text-right font-medium text-gray-500 dark:text-gray-400">
                            Tokens
                          </th>
                          <th className="py-3 px-2 text-right font-medium text-gray-500 dark:text-gray-400">
                            Cost
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {byModel.map((model) => (
                          <tr
                            key={model.model_id}
                            className="border-b border-gray-100 dark:border-gray-800/50"
                          >
                            <td className="py-3 px-2 font-mono text-gray-900 dark:text-gray-100">
                              {getModelShortName(model.model_id)}
                            </td>
                            <td className="py-3 px-2">
                              <Badge
                                variant={
                                  model.model_type === "free"
                                    ? "success"
                                    : "primary"
                                }
                                size="sm"
                              >
                                {model.model_type}
                              </Badge>
                            </td>
                            <td className="py-3 px-2 text-right text-gray-900 dark:text-gray-100">
                              {formatNumber(model.requests)}
                            </td>
                            <td className="py-3 px-2 text-right text-gray-900 dark:text-gray-100">
                              {formatNumber(model.tokens)}
                            </td>
                            <td className="py-3 px-2 text-right text-gray-900 dark:text-gray-100">
                              {formatCurrency(model.cost_usd, 6)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </CardContent>
              </Card>
            </div>
          </section>
        )}

        {/* Recent Logs */}
        <section>
          <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
            Recent Requests ({totalLogs} total)
          </h3>
          {logsLoading ? (
            <div className="space-y-3">
              {[...Array(3)].map((_, i) => (
                <Skeleton key={i} className="h-20" />
              ))}
            </div>
          ) : logs.length === 0 ? (
            <EmptyState
              icon={Activity}
              title="No requests yet"
              description="Requests will appear here once this project starts receiving traffic."
            />
          ) : (
            <>
              <div className="space-y-3">
                {logs.map((log) => (
                  <LogCard key={log.id} log={log} />
                ))}
              </div>
              {totalLogs > logsLimit && (
                <div className="mt-4 text-center">
                  <Button
                    variant="outline"
                    onClick={() => setLogsLimit(logsLimit + 20)}
                  >
                    Load More
                  </Button>
                </div>
              )}
            </>
          )}
        </section>
      </div>
    </div>
  );
}

function LogCard({ log }: { log: UsageLog }) {
  const [expanded, setExpanded] = useState(false);
  const [copied, setCopied] = useState(false);

  const handleCopyResponse = () => {
    if (log.response_content) {
      navigator.clipboard.writeText(log.response_content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <Card>
      <CardContent className="py-3">
        <div
          className="flex items-center gap-3 cursor-pointer"
          onClick={() => setExpanded(!expanded)}
        >
          <div
            className={`p-2 rounded-lg ${
              log.success
                ? "bg-green-100 dark:bg-green-900/30"
                : "bg-red-100 dark:bg-red-900/30"
            }`}
          >
            {log.success ? (
              <Activity className="h-4 w-4 text-green-600 dark:text-green-400" />
            ) : (
              <AlertCircle className="h-4 w-4 text-red-600 dark:text-red-400" />
            )}
          </div>

          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="font-medium text-gray-900 dark:text-gray-100">
                {getModelShortName(log.model_id)}
              </span>
              <Badge
                variant={log.model_type === "free" ? "success" : "primary"}
                size="sm"
              >
                {log.model_type}
              </Badge>
            </div>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
              {formatRelativeTime(log.created_at)} • {log.total_tokens} tokens
              • {log.response_time_ms}ms
            </p>
          </div>

          {expanded ? (
            <ChevronUp className="h-4 w-4 text-gray-400" />
          ) : (
            <ChevronDown className="h-4 w-4 text-gray-400" />
          )}
        </div>

        {expanded && (
          <div className="mt-4 pt-4 border-t border-gray-100 dark:border-gray-800 space-y-3">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
              <div>
                <p className="text-gray-500 dark:text-gray-400">Request ID</p>
                <p className="font-mono text-gray-900 dark:text-gray-100 text-xs mt-1">
                  {log.request_id || "N/A"}
                </p>
              </div>
              <div>
                <p className="text-gray-500 dark:text-gray-400">Input Tokens</p>
                <p className="text-gray-900 dark:text-gray-100 mt-1">
                  {formatNumber(log.input_tokens)}
                </p>
              </div>
              <div>
                <p className="text-gray-500 dark:text-gray-400">
                  Output Tokens
                </p>
                <p className="text-gray-900 dark:text-gray-100 mt-1">
                  {formatNumber(log.output_tokens)}
                </p>
              </div>
              <div>
                <p className="text-gray-500 dark:text-gray-400">Cost</p>
                <p className="text-gray-900 dark:text-gray-100 mt-1">
                  {formatCurrency(log.cost_usd, 8)}
                </p>
              </div>
            </div>

            {log.error_message && (
              <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
                <p className="text-sm text-red-700 dark:text-red-300">
                  {log.error_message}
                </p>
              </div>
            )}

            {log.response_content && (
              <div>
                <div className="flex items-center justify-between mb-2">
                  <p className="text-sm font-medium text-gray-500 dark:text-gray-400">
                    Response Preview
                  </p>
                  <Button variant="ghost" size="sm" onClick={handleCopyResponse}>
                    {copied ? (
                      <Check className="h-3 w-3" />
                    ) : (
                      <Copy className="h-3 w-3" />
                    )}
                  </Button>
                </div>
                <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded-lg max-h-40 overflow-y-auto">
                  <p className="text-sm text-gray-700 dark:text-gray-300 whitespace-pre-wrap">
                    {log.response_content.length > 500
                      ? `${log.response_content.substring(0, 500)}...`
                      : log.response_content}
                  </p>
                </div>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
