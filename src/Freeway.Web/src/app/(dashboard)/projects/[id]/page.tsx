"use client";

import { useMemo, useState } from "react";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import {
  ArrowLeft,
  FolderKanban,
  Activity,
  Zap,
  DollarSign,
  Clock,
  CircleAlert,
  ChevronDown,
  Copy,
  Check,
  Coins,
} from "lucide-react";
import { Header } from "@/components/layout/header";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Stat, StatStrip } from "@/components/ui/stat-card";
import { Skeleton, SkeletonStrip, SkeletonList } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/empty-state";
import { projectsApi } from "@/lib/api/projects";
import { analyticsApi } from "@/lib/api/analytics";
import {
  formatNumber,
  formatSpend,
  formatDateTime,
  formatRelativeTime,
  getModelShortName,
  startOfMonthIso,
  daysAgoIso,
  currentMonthLabel,
} from "@/lib/utils/format";
import { cn } from "@/lib/utils/cn";
import { ACCENTS, accentForUsage, usageLabel, type AccentKey } from "@/lib/theme/accents";
import type { ModelUsageStats, UsageLog } from "@/lib/types";

type Period = "month" | "7d" | "all";

const PERIODS: { key: Period; label: string; range: () => string | undefined }[] = [
  { key: "month", label: "This month", range: () => startOfMonthIso() },
  { key: "7d", label: "Last 7 days", range: () => daysAgoIso(7) },
  { key: "all", label: "All time", range: () => undefined },
];

export default function ProjectDetailsPage() {
  const params = useParams();
  const projectId = params.id as string;

  const [period, setPeriod] = useState<Period>("month");
  const [logsLimit, setLogsLimit] = useState(20);

  const startDate = PERIODS.find((p) => p.key === period)!.range();

  const projectQuery = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => projectsApi.getProject(projectId),
  });

  const usageQuery = useQuery({
    queryKey: ["project-usage", projectId, period],
    queryFn: () => analyticsApi.getProjectUsage(projectId, startDate),
  });

  // Kept separate from the period so lifetime spend is always on screen.
  const lifetimeQuery = useQuery({
    queryKey: ["project-usage", projectId, "all"],
    queryFn: () => analyticsApi.getProjectUsage(projectId),
  });

  const monthQuery = useQuery({
    queryKey: ["project-usage", projectId, "month"],
    queryFn: () => analyticsApi.getProjectUsage(projectId, startOfMonthIso()),
  });

  const logsQuery = useQuery({
    queryKey: ["project-logs", projectId, logsLimit],
    queryFn: () =>
      analyticsApi.getUsageLogs(projectId, { limit: logsLimit, offset: 0 }),
  });

  const project = projectQuery.data;
  const summary = usageQuery.data?.summary;
  const byModel = useMemo(
    () => [...(usageQuery.data?.by_model ?? [])].sort((a, b) => b.requests - a.requests),
    [usageQuery.data]
  );
  const logs = logsQuery.data?.logs || [];
  const totalLogs = logsQuery.data?.total_count || 0;

  if (projectQuery.isLoading) {
    return (
      <div className="flex h-full flex-col">
        <Header title="Loading project" />
        <div className="space-y-6 p-4 md:p-6">
          <SkeletonStrip />
          <SkeletonList count={2} />
        </div>
      </div>
    );
  }

  if (projectQuery.isError || !project) {
    return (
      <div className="flex h-full flex-col">
        <Header title="Project not found" />
        <div className="flex-1 p-4 md:p-6">
          <EmptyState
            className="accent-brand"
            icon={FolderKanban}
            title="That project is not here"
            description="It may have been deleted, or the link may be wrong."
            action={
              <Link href="/projects">
                <Button variant="outline">
                  <ArrowLeft className="h-4 w-4" aria-hidden />
                  Back to projects
                </Button>
              </Link>
            }
          />
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col">
      <Header
        title={project.name}
        subtitle={`Key ${project.api_key_prefix}… · ${project.rate_limit_per_minute} requests/min`}
        actions={
          <Badge variant={project.is_active ? "success" : "default"} size="md" dot>
            {project.is_active ? "Active" : "Paused"}
          </Badge>
        }
      />

      <div className="flex-1 space-y-6 p-4 md:p-6">
        <Link
          href="/projects"
          className="inline-flex items-center gap-1.5 rounded-control text-sm text-muted transition-colors hover:text-ink"
        >
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
          All projects
        </Link>

        <SpendPanel
          monthCost={monthQuery.data?.summary?.total_cost_usd}
          lifetimeCost={lifetimeQuery.data?.summary?.total_cost_usd}
          monthRequests={monthQuery.data?.summary?.total_requests}
          lifetimeRequests={lifetimeQuery.data?.summary?.total_requests}
          isLoading={monthQuery.isLoading || lifetimeQuery.isLoading}
        />

        <section aria-labelledby="usage-heading" className="space-y-3">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2
                id="usage-heading"
                className="text-base font-semibold tracking-tight text-ink"
              >
                Usage
              </h2>
              <p className="text-sm text-muted">
                {PERIODS.find((p) => p.key === period)!.label.toLowerCase()}
              </p>
            </div>
            <div
              role="tablist"
              aria-label="Time period"
              className="flex items-center gap-0.5 rounded-control bg-inset p-0.5"
            >
              {PERIODS.map((p) => (
                <button
                  key={p.key}
                  role="tab"
                  aria-selected={period === p.key}
                  onClick={() => setPeriod(p.key)}
                  className={cn(
                    "rounded-[0.3125rem] px-3 py-1.5 text-sm font-medium transition-colors duration-150 ease-swift",
                    period === p.key
                      ? "bg-panel text-ink shadow-pop"
                      : "text-muted hover:text-ink"
                  )}
                >
                  {p.label}
                </button>
              ))}
            </div>
          </div>

          {usageQuery.isLoading ? (
            <SkeletonStrip />
          ) : usageQuery.isError ? (
            <ErrorState
              title="Could not load usage"
              onRetry={() => usageQuery.refetch()}
            />
          ) : summary && summary.total_requests > 0 ? (
            <StatStrip className="stagger">
              <Stat
                label="Requests"
                value={formatNumber(summary.total_requests)}
                icon={Zap}
                accent={ACCENTS.low.scope}
                note={`${formatNumber(summary.failed_requests)} failed`}
              />
              <Stat
                label="Success rate"
                value={`${(summary.success_rate ?? 0).toFixed(1)}%`}
                icon={Activity}
                accent={ACCENTS.free.scope}
              />
              <Stat
                label="Tokens"
                value={formatNumber(summary.total_tokens)}
                icon={Coins}
                accent={ACCENTS.moderate.scope}
                note={`${formatNumber(summary.total_input_tokens)} in / ${formatNumber(summary.total_output_tokens)} out`}
              />
              <Stat
                label="Avg response"
                value={`${(summary.avg_response_time_ms ?? 0).toFixed(0)}ms`}
                icon={Clock}
                accent={ACCENTS.image.scope}
              />
            </StatStrip>
          ) : (
            <EmptyState
              className="accent-brand"
              icon={Activity}
              title="No traffic in this period"
              description="Send a request with this project's API key and it will show up here."
            />
          )}
        </section>

        {byModel.length > 0 && (
          <ModelBreakdown models={byModel} />
        )}

        <section aria-labelledby="logs-heading" className="space-y-3">
          <div>
            <h2
              id="logs-heading"
              className="text-base font-semibold tracking-tight text-ink"
            >
              Recent requests
            </h2>
            <p className="text-sm text-muted">
              {formatNumber(totalLogs)} logged for this project
            </p>
          </div>

          {logsQuery.isLoading ? (
            <SkeletonList count={3} />
          ) : logsQuery.isError ? (
            <ErrorState
              title="Could not load request logs"
              onRetry={() => logsQuery.refetch()}
            />
          ) : logs.length === 0 ? (
            <EmptyState
              className="accent-brand"
              icon={Activity}
              title="No requests logged yet"
              description="Every call through this key gets recorded here with its model, tokens and cost."
            />
          ) : (
            <>
              <div className="grid gap-2">
                {logs.map((log) => (
                  <LogRow key={log.id} log={log} />
                ))}
              </div>
              {totalLogs > logsLimit && (
                <div className="flex justify-center pt-1">
                  <Button variant="outline" onClick={() => setLogsLimit(logsLimit + 20)}>
                    Show 20 more
                  </Button>
                </div>
              )}
            </>
          )}
        </section>

        <Card>
          <CardHeader>
            <CardTitle>Project details</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div>
                <dt className="text-xs text-subtle">Project ID</dt>
                <dd className="mt-1 break-all font-mono text-xs text-ink">{project.id}</dd>
              </div>
              <div>
                <dt className="text-xs text-subtle">API key</dt>
                <dd className="mt-1 font-mono text-sm text-ink">
                  {project.api_key_prefix}…
                </dd>
              </div>
              <div>
                <dt className="text-xs text-subtle">Rate limit</dt>
                <dd className="tnum mt-1 text-sm text-ink">
                  {project.rate_limit_per_minute} req/min
                </dd>
              </div>
              <div>
                <dt className="text-xs text-subtle">Created</dt>
                <dd className="mt-1 text-sm text-ink">
                  {formatDateTime(project.created_at)}
                </dd>
              </div>
            </dl>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

/** Lifetime and month-to-date spend side by side, since one without the other misleads. */
function SpendPanel({
  monthCost,
  lifetimeCost,
  monthRequests,
  lifetimeRequests,
  isLoading,
}: {
  monthCost?: number;
  lifetimeCost?: number;
  monthRequests?: number;
  lifetimeRequests?: number;
  isLoading: boolean;
}) {
  return (
    <Card className="overflow-hidden">
      <div className="grid sm:grid-cols-2 sm:divide-x divide-y sm:divide-y-0 divide-line">
        <div className="accent-premium accent-wash p-5">
          <div className="flex items-center gap-2">
            <span className="grid h-6 w-6 place-items-center rounded-[0.3125rem] accent-tint accent-text">
              <DollarSign className="h-3.5 w-3.5" aria-hidden />
            </span>
            <p className="text-sm text-muted">Spent in {currentMonthLabel()}</p>
          </div>
          {isLoading ? (
            <Skeleton className="mt-2 h-9 w-32" />
          ) : (
            <>
              <p className="metric mt-2 text-3xl font-semibold text-ink">
                {formatSpend(monthCost)}
              </p>
              <p className="mt-1 text-xs text-subtle">
                {formatNumber(monthRequests ?? 0)} requests this month
              </p>
            </>
          )}
        </div>

        <div className="accent-brand p-5">
          <div className="flex items-center gap-2">
            <span className="grid h-6 w-6 place-items-center rounded-[0.3125rem] accent-tint accent-text">
              <Coins className="h-3.5 w-3.5" aria-hidden />
            </span>
            <p className="text-sm text-muted">Spent all time</p>
          </div>
          {isLoading ? (
            <Skeleton className="mt-2 h-9 w-32" />
          ) : (
            <>
              <p className="metric mt-2 text-3xl font-semibold text-ink">
                {formatSpend(lifetimeCost)}
              </p>
              <p className="mt-1 text-xs text-subtle">
                {formatNumber(lifetimeRequests ?? 0)} requests since the project was created
              </p>
            </>
          )}
        </div>
      </div>
    </Card>
  );
}

/**
 * Ranked bars beat a pie here: the job is comparing magnitudes across models,
 * and a pie with eight slices cannot be read. Colour encodes the model's type,
 * matching the lanes everywhere else, and each bar is directly labelled so the
 * hue is never the only thing carrying identity.
 */
function ModelBreakdown({ models }: { models: ModelUsageStats[] }) {
  const max = Math.max(...models.map((m) => m.requests), 1);
  const totalRequests = models.reduce((sum, m) => sum + m.requests, 0);

  const typesPresent = Array.from(
    new Set(models.map((m) => accentForUsage(m.model_type, m.model_tier)))
  );

  return (
    <section aria-labelledby="models-heading" className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2
            id="models-heading"
            className="text-base font-semibold tracking-tight text-ink"
          >
            Where the requests went
          </h2>
          <p className="text-sm text-muted">
            {models.length} {models.length === 1 ? "model" : "models"} used
          </p>
        </div>
        <ul className="flex flex-wrap items-center gap-x-4 gap-y-1.5">
          {typesPresent.map((key) => (
            <li key={key} className={cn(ACCENTS[key].scope, "flex items-center gap-1.5")}>
              <span className="h-2 w-2 rounded-full mark-bg" aria-hidden />
              <span className="text-xs text-muted">{ACCENTS[key].label}</span>
            </li>
          ))}
        </ul>
      </div>

      <Card>
        <CardContent className="space-y-3.5">
          {models.map((model) => {
            const key = accentForUsage(model.model_type, model.model_tier);
            const share = totalRequests ? (model.requests / totalRequests) * 100 : 0;
            return (
              <div key={`${model.model_id}-${model.model_tier ?? ""}`} className={ACCENTS[key].scope}>
                <div className="flex items-baseline justify-between gap-3">
                  <span className="truncate text-sm font-medium text-ink">
                    {getModelShortName(model.model_id)}
                  </span>
                  <span className="metric shrink-0 text-sm text-muted">
                    {formatNumber(model.requests)}
                    <span className="ml-1.5 text-subtle">{share.toFixed(0)}%</span>
                  </span>
                </div>
                <div
                  className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-inset"
                  role="img"
                  aria-label={`${getModelShortName(model.model_id)}: ${model.requests} requests, ${share.toFixed(0)} percent`}
                >
                  <div
                    className="h-full rounded-full mark-bg transition-[width] duration-500 ease-swift"
                    style={{ width: `${Math.max((model.requests / max) * 100, 2)}%` }}
                  />
                </div>
                <div className="mt-1 flex items-center gap-2 text-xs text-subtle">
                  <Badge variant="accent">{usageLabel(model.model_type, model.model_tier)}</Badge>
                  <span className="tnum">{formatNumber(model.tokens)} tokens</span>
                  <span aria-hidden>·</span>
                  <span className="tnum">{formatSpend(model.cost_usd)}</span>
                </div>
              </div>
            );
          })}
        </CardContent>
      </Card>
    </section>
  );
}

function LogRow({ log }: { log: UsageLog }) {
  const [expanded, setExpanded] = useState(false);
  const [copied, setCopied] = useState(false);
  const accentKey: AccentKey = accentForUsage(log.model_type, log.model_tier);

  const handleCopyResponse = async () => {
    if (!log.response_content) return;
    try {
      await navigator.clipboard.writeText(log.response_content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  return (
    <Card rail className={cn(ACCENTS[accentKey].scope, "overflow-hidden")}>
      <button
        onClick={() => setExpanded(!expanded)}
        aria-expanded={expanded}
        className="flex w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-inset/60"
      >
        <span
          className={cn(
            "grid h-7 w-7 shrink-0 place-items-center rounded-control",
            log.success ? "bg-ok/12 text-ok" : "bg-danger/12 text-danger"
          )}
        >
          {log.success ? (
            <Activity className="h-3.5 w-3.5" aria-hidden />
          ) : (
            <CircleAlert className="h-3.5 w-3.5" aria-hidden />
          )}
        </span>

        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-2">
            <span className="truncate text-sm font-medium text-ink">
              {getModelShortName(log.model_id)}
            </span>
            <Badge variant="accent">{usageLabel(log.model_type, log.model_tier)}</Badge>
            {!log.success && <Badge variant="error">Failed</Badge>}
          </span>
          <span className="mt-0.5 flex flex-wrap items-center gap-x-3 text-xs text-subtle">
            <span>{formatRelativeTime(log.created_at)}</span>
            <span className="tnum">{formatNumber(log.total_tokens)} tokens</span>
            <span className="tnum">{log.response_time_ms}ms</span>
            <span className="tnum">{formatSpend(log.cost_usd)}</span>
          </span>
        </span>

        <ChevronDown
          className={cn(
            "h-4 w-4 shrink-0 text-subtle transition-transform duration-200 ease-swift",
            expanded && "rotate-180"
          )}
          aria-hidden
        />
      </button>

      {expanded && (
        <div className="space-y-4 border-t border-line px-4 py-4 animate-rise-in">
          <dl className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <div>
              <dt className="text-xs text-subtle">Request ID</dt>
              <dd className="mt-0.5 break-all font-mono text-xs text-ink">
                {log.request_id || "—"}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-subtle">Input tokens</dt>
              <dd className="tnum mt-0.5 text-sm text-ink">
                {formatNumber(log.input_tokens)}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-subtle">Output tokens</dt>
              <dd className="tnum mt-0.5 text-sm text-ink">
                {formatNumber(log.output_tokens)}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-subtle">Cost</dt>
              <dd className="tnum mt-0.5 text-sm text-ink">{formatSpend(log.cost_usd)}</dd>
            </div>
          </dl>

          {log.error_message && (
            <div className="rounded-control border border-danger/30 bg-danger/[0.06] px-3.5 py-3">
              <p className="text-sm text-danger">{log.error_message}</p>
            </div>
          )}

          {log.response_content && (
            <div>
              <div className="mb-2 flex items-center justify-between">
                <p className="text-xs font-medium text-muted">Response</p>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleCopyResponse}
                  aria-label={copied ? "Copied" : "Copy response"}
                >
                  {copied ? (
                    <>
                      <Check className="h-3.5 w-3.5 text-ok" aria-hidden />
                      Copied
                    </>
                  ) : (
                    <>
                      <Copy className="h-3.5 w-3.5" aria-hidden />
                      Copy
                    </>
                  )}
                </Button>
              </div>
              <div className="max-h-44 overflow-y-auto rounded-control border border-line bg-inset px-3.5 py-3">
                <p className="whitespace-pre-wrap text-sm text-muted">
                  {log.response_content.length > 500
                    ? `${log.response_content.slice(0, 500)}…`
                    : log.response_content}
                </p>
              </div>
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
