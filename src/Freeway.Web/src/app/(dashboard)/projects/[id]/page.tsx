"use client";

import { useMemo, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, ChevronDown, CircleAlert, Copy, Check, FolderKanban } from "lucide-react";
import { PageHead } from "@/components/shell/page-head";
import { Panel, PanelHead, Rule } from "@/components/ui/panel";
import { Button } from "@/components/ui/button";
import { Tag } from "@/components/ui/tag";
import { Segmented } from "@/components/ui/segmented";
import { Pulse, PulseRows } from "@/components/ui/pulse";
import { Empty, Failed } from "@/components/ui/state";
import { projectsApi } from "@/lib/api/projects";
import { analyticsApi } from "@/lib/api/analytics";
import { laneOf, laneLabel } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";
import {
  ago,
  compact,
  count,
  currentMonthLabel,
  dateTime,
  daysAgoIso,
  modelShortName,
  money,
  ms,
  pct,
  startOfMonthIso,
} from "@/lib/utils/format";
import type { ModelUsageStats, UsageLog } from "@/lib/types";

type Period = "month" | "7d" | "all";

const PERIODS: { value: Period; label: string; from: () => string | undefined }[] = [
  { value: "month", label: "Month", from: () => startOfMonthIso() },
  { value: "7d", label: "7 days", from: () => daysAgoIso(7) },
  { value: "all", label: "All time", from: () => undefined },
];

export default function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [period, setPeriod] = useState<Period>("month");
  const [limit, setLimit] = useState(20);

  const from = PERIODS.find((p) => p.value === period)!.from();

  const project = useQuery({ queryKey: ["project", id], queryFn: () => projectsApi.getProject(id) });
  const usage = useQuery({
    queryKey: ["project-usage", id, period],
    queryFn: () => analyticsApi.getProjectUsage(id, from),
  });
  const month = useQuery({
    queryKey: ["project-usage", id, "month"],
    queryFn: () => analyticsApi.getProjectUsage(id, startOfMonthIso()),
  });
  const life = useQuery({
    queryKey: ["project-usage", id, "all"],
    queryFn: () => analyticsApi.getProjectUsage(id),
  });
  const logs = useQuery({
    queryKey: ["project-logs", id, limit],
    queryFn: () => analyticsApi.getUsageLogs(id, { limit, offset: 0 }),
  });

  const byModel = useMemo(
    () => [...(usage.data?.by_model ?? [])].sort((a, b) => b.requests - a.requests),
    [usage.data]
  );

  if (project.isLoading) {
    return (
      <div className="space-y-6">
        <Pulse className="h-10 w-64" />
        <Pulse className="h-40 w-full" />
      </div>
    );
  }

  if (project.isError || !project.data) {
    return (
      <Panel>
        <Empty
          className="lane-accent"
          icon={FolderKanban}
          title="That project is not here"
          body="It may have been deleted, or the link may be wrong."
          action={
            <Link href="/projects">
              <Button>
                <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
                All projects
              </Button>
            </Link>
          }
        />
      </Panel>
    );
  }

  const p = project.data;
  const s = usage.data?.summary;

  return (
    <div className="space-y-7">
      <div>
        <Link
          href="/projects"
          className="mb-3 inline-flex items-center gap-1.5 rounded text-xs text-text-3 transition-colors hover:text-text"
        >
          <ArrowLeft className="h-3 w-3" aria-hidden />
          All projects
        </Link>
        <PageHead
          title={p.name}
          lede={
            <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
              <code className="font-mono text-xs">{p.api_key_prefix}…</code>
              <span className="fig text-xs">{p.rate_limit_per_minute}/min</span>
              <span className="text-xs">created {dateTime(p.created_at)}</span>
            </span>
          }
          actions={
            <Tag tone={p.is_active ? "ok" : "neutral"} dot>
              {p.is_active ? "accepting requests" : "paused"}
            </Tag>
          }
        />
      </div>

      {/* Lifetime and month-to-date together: one without the other misleads. */}
      <section className="enter grid gap-4 sm:grid-cols-2">
        <Panel className="lane-premium lane-halo p-5">
          <p className="text-2xs font-medium uppercase text-text-3">
            Spent in {currentMonthLabel()}
          </p>
          {month.isLoading ? (
            <Pulse className="mt-3 h-11 w-36" />
          ) : (
            <p className="fig-hero mt-3 text-4xl text-text">
              {money(Number(month.data?.summary?.total_cost_usd ?? 0))}
            </p>
          )}
          <p className="mt-2 text-xs text-text-3">
            {compact(month.data?.summary?.total_requests ?? 0)} requests this month
          </p>
        </Panel>

        <Panel className="lane-accent p-5">
          <p className="text-2xs font-medium uppercase text-text-3">Spent all time</p>
          {life.isLoading ? (
            <Pulse className="mt-3 h-11 w-36" />
          ) : (
            <p className="fig-hero mt-3 text-4xl text-text">
              {money(Number(life.data?.summary?.total_cost_usd ?? 0))}
            </p>
          )}
          <p className="mt-2 text-xs text-text-3">
            {compact(life.data?.summary?.total_requests ?? 0)} requests since this project was created
          </p>
        </Panel>
      </section>

      <section>
        <Rule
          action={
            <Segmented
              label="Period"
              value={period}
              onChange={setPeriod}
              segments={PERIODS.map((x) => ({ value: x.value, label: x.label }))}
            />
          }
        >
          Usage
        </Rule>

        {usage.isError ? (
          <Failed title="Could not load usage" onRetry={() => usage.refetch()} />
        ) : usage.isLoading ? (
          <Panel>
            <PulseRows rows={2} />
          </Panel>
        ) : !s || s.total_requests === 0 ? (
          <Panel>
            <Empty
              className="lane-accent"
              icon={CircleAlert}
              title="No traffic in this period"
              body="Send a request with this project's key and it will show up here."
            />
          </Panel>
        ) : (
          <Panel>
            <dl className="grid grid-cols-2 divide-x divide-y divide-hair sm:grid-cols-4 sm:divide-y-0">
              <Stat label="Requests" value={compact(s.total_requests)} note={`${count(s.failed_requests)} failed`} />
              <Stat label="Success" value={pct(s.success_rate, 1)} tone={s.success_rate < 95 ? "bad" : undefined} />
              <Stat
                label="Tokens"
                value={compact(s.total_tokens)}
                note={`${compact(s.total_input_tokens)} in · ${compact(s.total_output_tokens)} out`}
              />
              <Stat label="Avg latency" value={ms(s.avg_response_time_ms)} />
            </dl>
          </Panel>
        )}
      </section>

      {byModel.length > 0 && <ModelBreakdown models={byModel} />}

      <section>
        <Rule>Requests · {compact(logs.data?.total_count ?? 0)} logged</Rule>
        {logs.isError ? (
          <Failed title="Could not load request logs" onRetry={() => logs.refetch()} />
        ) : logs.isLoading ? (
          <Panel>
            <PulseRows rows={5} />
          </Panel>
        ) : (logs.data?.logs.length ?? 0) === 0 ? (
          <Panel>
            <Empty
              className="lane-accent"
              icon={CircleAlert}
              title="Nothing logged yet"
              body="Every call through this key gets recorded with its lane, tokens and cost."
            />
          </Panel>
        ) : (
          <>
            <Panel>
              <ul className="divide-y divide-hair">
                {logs.data!.logs.map((l) => (
                  <LogRow key={l.id} log={l} />
                ))}
              </ul>
            </Panel>
            {(logs.data?.total_count ?? 0) > limit && (
              <div className="mt-3 flex justify-center">
                <Button onClick={() => setLimit(limit + 20)}>Show 20 more</Button>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  );
}

function Stat({
  label,
  value,
  note,
  tone,
}: {
  label: string;
  value: string;
  note?: string;
  tone?: "bad";
}) {
  return (
    <div className="p-4">
      <dt className="text-2xs font-medium uppercase text-text-3">{label}</dt>
      <dd className={cn("fig mt-1.5 text-2xl", tone === "bad" ? "text-bad" : "text-text")}>{value}</dd>
      {note && <p className="mt-1 text-xs text-text-3">{note}</p>}
    </div>
  );
}

/**
 * Ranked bars rather than a pie: the job is comparing magnitudes across models, and
 * a pie stops being readable past a few slices. Colour encodes the lane, and every
 * bar is directly labelled so hue is never the only carrier of identity.
 */
function ModelBreakdown({ models }: { models: ModelUsageStats[] }) {
  const max = Math.max(...models.map((m) => m.requests), 1);
  const total = models.reduce((sum, m) => sum + m.requests, 0);

  return (
    <section>
      <Rule>Where the requests went</Rule>
      <Panel>
        <ul className="divide-y divide-hair">
          {models.map((m) => {
            const lane = laneOf(m.model_type, m.model_tier);
            const share = total ? (m.requests / total) * 100 : 0;
            return (
              <li key={`${m.model_id}-${m.model_tier ?? ""}`} className={cn(lane.scope, "px-4 py-3")}>
                <div className="flex items-baseline justify-between gap-3">
                  <span className="truncate font-mono text-xs text-text">
                    {modelShortName(m.model_id)}
                  </span>
                  <span className="fig shrink-0 text-xs text-text-2">
                    {compact(m.requests)}
                    <span className="ml-2 text-text-3">{pct(share)}</span>
                  </span>
                </div>
                <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-sunken">
                  <div
                    className="h-full rounded-full lane-bg transition-[width] duration-500 ease-out"
                    style={{ width: `${Math.max((m.requests / max) * 100, 2)}%` }}
                  />
                </div>
                <div className="mt-1.5 flex items-center gap-3">
                  <Tag tone="lane" mono>
                    {laneLabel(m.model_type, m.model_tier)}
                  </Tag>
                  <span className="fig text-2xs text-text-3">{compact(m.tokens)} tokens</span>
                  <span className="fig text-2xs text-text-3">{money(Number(m.cost_usd))}</span>
                </div>
              </li>
            );
          })}
        </ul>
      </Panel>
    </section>
  );
}

function LogRow({ log }: { log: UsageLog }) {
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const lane = laneOf(log.model_type, log.model_tier);

  const copy = async () => {
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
    <li className={lane.scope}>
      <button
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        className="flex w-full items-center gap-3 px-4 py-2.5 text-left transition-colors hover:bg-raised"
      >
        {log.success ? (
          <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-ok" aria-label="ok" />
        ) : (
          <CircleAlert className="h-3.5 w-3.5 shrink-0 text-bad" aria-label="failed" />
        )}
        <span className="min-w-0 flex-1 truncate font-mono text-xs text-text">
          {modelShortName(log.model_id)}
        </span>
        <Tag tone="lane" mono className="hidden shrink-0 sm:inline-flex">
          {laneLabel(log.model_type, log.model_tier)}
        </Tag>
        <span className="fig hidden w-16 shrink-0 text-right text-xs text-text-3 sm:block">
          {compact(log.total_tokens)}
        </span>
        <span className="fig w-14 shrink-0 text-right text-xs text-text-3">
          {ms(log.response_time_ms)}
        </span>
        <span className="fig w-20 shrink-0 text-right text-xs text-text-2">
          {money(Number(log.cost_usd))}
        </span>
        <span className="hidden w-16 shrink-0 text-right text-xs text-text-3 md:block">
          {ago(log.created_at)}
        </span>
        <ChevronDown
          className={cn("h-3.5 w-3.5 shrink-0 text-text-3 transition-transform", open && "rotate-180")}
          aria-hidden
        />
      </button>

      {open && (
        <div className="space-y-4 border-t border-hair bg-sunken px-4 py-4 animate-lift">
          <dl className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <Detail label="Request ID" value={log.request_id || "—"} mono />
            <Detail label="Input tokens" value={count(log.input_tokens)} />
            <Detail label="Output tokens" value={count(log.output_tokens)} />
            <Detail label="Model" value={log.model_id} mono />
          </dl>

          {log.error_message && (
            <div className="rounded border border-bad/30 bg-bad/[0.07] px-3.5 py-2.5">
              <p className="text-sm text-bad">{log.error_message}</p>
            </div>
          )}

          {log.response_content && (
            <div>
              <div className="mb-2 flex items-center justify-between">
                <p className="text-2xs font-medium uppercase text-text-3">Response</p>
                <Button size="sm" variant="ghost" onClick={copy}>
                  {copied ? (
                    <>
                      <Check className="h-3 w-3 text-ok" aria-hidden />
                      Copied
                    </>
                  ) : (
                    <>
                      <Copy className="h-3 w-3" aria-hidden />
                      Copy
                    </>
                  )}
                </Button>
              </div>
              <div className="max-h-44 overflow-y-auto rounded border border-hair bg-panel px-3.5 py-3">
                <p className="whitespace-pre-wrap text-sm text-text-2">
                  {log.response_content.length > 600
                    ? `${log.response_content.slice(0, 600)}…`
                    : log.response_content}
                </p>
              </div>
            </div>
          )}
        </div>
      )}
    </li>
  );
}

function Detail({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="min-w-0">
      <dt className="text-2xs uppercase text-text-3">{label}</dt>
      <dd className={cn("mt-0.5 truncate text-xs text-text", mono && "font-mono")}>{value}</dd>
    </div>
  );
}
