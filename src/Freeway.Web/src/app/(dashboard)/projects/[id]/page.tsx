"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Check, ChevronDown, CircleAlert, Copy, FolderKanban } from "lucide-react";
import { Panel, Rule } from "@/components/ui/panel";
import { Button } from "@/components/ui/button";
import { Tag } from "@/components/ui/tag";
import { Segmented } from "@/components/ui/segmented";
import { Pulse } from "@/components/ui/pulse";
import { Empty, Failed } from "@/components/ui/state";
import { AnimatedNumber, Delta, LaneDot, Meter, Sparkline } from "@/components/charts/primitives";
import { LaneChart } from "@/components/charts/lane-chart";
import { TrafficMap } from "@/components/charts/traffic-map";
import { ModelsTable } from "@/components/overview/sections";
import { projectsApi } from "@/lib/api/projects";
import { analyticsApi } from "@/lib/api/analytics";
import { laneOf } from "@/lib/theme/lanes";
import { backendOf } from "@/lib/theme/backends";
import { cn } from "@/lib/utils/cn";
import { ago, cents, compact, count, dateTime, fullDate, modelShortName, money, ms, pct } from "@/lib/utils/format";
import type { UsageLog } from "@/lib/types";

type Window = "7" | "30" | "90";

export default function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [days, setDays] = useState<Window>("30");
  const [limit, setLimit] = useState(25);

  const project = useQuery({ queryKey: ["project", id], queryFn: () => projectsApi.getProject(id) });
  const overview = useQuery({
    queryKey: ["overview", days, id],
    queryFn: () => analyticsApi.getOverview(Number(days), id),
    refetchInterval: 30_000,
    placeholderData: (prev) => prev,
  });
  const life = useQuery({ queryKey: ["project-usage", id, "all"], queryFn: () => analyticsApi.getProjectUsage(id) });
  const logs = useQuery({
    queryKey: ["project-logs", id, limit],
    queryFn: () => analyticsApi.getUsageLogs(id, { limit, offset: 0 }),
    refetchInterval: 30_000,
    placeholderData: (prev) => prev,
  });

  if (project.isLoading) {
    return (
      <div className="space-y-6">
        <Pulse className="h-12 w-72" />
        <Pulse className="h-44 w-full" />
      </div>
    );
  }

  if (project.isError || !project.data) {
    return (
      <Panel>
        <Empty
          icon={FolderKanban}
          title="That project is not here"
          body="It may have been deleted, or the link may be wrong."
          action={
            <Link href="/projects">
              <Button>
                <ArrowLeft className="h-4 w-4" aria-hidden />
                All projects
              </Button>
            </Link>
          }
        />
      </Panel>
    );
  }

  const p = project.data;
  const o = overview.data;
  const w = o?.window;
  const prev = o?.previous_window;
  const series = o?.series ?? [];
  const lifeSummary = life.data?.summary;

  return (
    <div className="space-y-10">
      <div className="reveal">
        <Link
          href="/projects"
          className="mb-5 inline-flex items-center gap-2 rounded text-base font-semibold text-text-3 transition-colors hover:text-text"
        >
          <ArrowLeft className="h-4 w-4" aria-hidden />
          All projects
        </Link>
        <div className="flex flex-wrap items-end justify-between gap-6">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-3xl font-extrabold text-text sm:text-4xl">{p.name}</h1>
              <Tag tone={p.is_active ? "ok" : "neutral"} dot>
                {p.is_active ? "Accepting requests" : "Paused"}
              </Tag>
            </div>
            <p className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-1 text-base text-text-3">
              <code className="font-mono text-text-2">{p.api_key_prefix}…</code>
              <span className="tnum">{p.rate_limit_per_minute} requests per minute</span>
              <span>Created {fullDate(p.created_at)}</span>
            </p>
          </div>
          <Segmented
            label="Time window"
            value={days}
            onChange={setDays}
            segments={[
              { value: "7", label: "7 days" },
              { value: "30", label: "30 days" },
              { value: "90", label: "90 days" },
            ]}
          />
        </div>
      </div>

      {overview.isError ? (
        <Failed title="Could not load this project's figures" onRetry={() => overview.refetch()} />
      ) : (
        <div className={cn("space-y-10 transition-opacity duration-300", overview.isFetching && overview.isPlaceholderData && "opacity-60")}>
          <section className="reveal grid gap-4 sm:grid-cols-2 xl:grid-cols-5" style={{ ["--d" as string]: "80ms" }}>
            <Tile tint="tint-sky" label="Requests" value={w ? <AnimatedNumber value={w.requests} format={(v) => count(Math.round(v))} /> : null}
              foot={w && prev ? <Delta current={w.requests} previous={prev.requests} /> : null}
              trend={series.map((s) => s.requests)}
              note={w ? `${pct(w.requests ? ((w.requests - w.failures) / w.requests) * 100 : 100, 1)} succeeded` : ""} />
            <Tile tint="tint-coral" label="Billed" value={w ? <AnimatedNumber value={Number(w.cost)} format={cents} /> : null}
              foot={w && prev ? <Delta current={Number(w.cost)} previous={Number(prev.cost)} upIsGood={false} /> : null}
              trend={series.map((s) => Number(s.cost))} note="Charged by OpenRouter" />
            <Tile tint="tint-pink" label="Credit used" value={w ? <AnimatedNumber value={Number(w.credit_usd)} format={cents} /> : null}
              trend={series.map((s) => Number(s.credit_usd ?? 0))} note="Taken off the Claude credit" />
            <Tile tint="tint-mint" label="Answered free" value={w ? <AnimatedNumber value={w.requests ? (w.unbilled / w.requests) * 100 : 0} format={(v) => `${v.toFixed(0)}%`} /> : null}
              foot={w ? <Meter value={w.requests ? (w.unbilled / w.requests) * 100 : 0} warnAt={101} badAt={101} label="Share answered without a bill" className="mt-2" /> : null}
              note={w ? `${count(w.unbilled)} without a bill` : ""} />
            <Tile tint="tint-violet" label="Response time" value={w ? <AnimatedNumber value={w.p50_response_ms} format={(v) => ms(v)} /> : null}
              note={w ? `Median. 95% within ${ms(w.p95_response_ms)}` : ""} />
          </section>

          <section className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
            <Panel className="reveal" style={{ ["--d" as string]: "160ms" }}>
              {o ? <TrafficMap flows={o.flows} days={o.days} compactLabels height={360} /> : <Pulse className="m-6 h-80" />}
            </Panel>
            <Panel className="reveal flex flex-col justify-between p-6" style={{ ["--d" as string]: "200ms" }}>
              <div>
                <p className="text-lg font-bold text-text">Since it was created</p>
                <p className="mt-1 text-sm text-text-3">Every request this project has ever made</p>
              </div>
              <dl className="mt-6 space-y-5">
                <LifeRow label="Requests" value={lifeSummary ? count(lifeSummary.total_requests) : null} />
                <LifeRow label="Billed" value={lifeSummary ? cents(Number(lifeSummary.total_cost_usd)) : null} />
                <LifeRow label="Tokens" value={lifeSummary ? compact(lifeSummary.total_tokens) : null} />
                <LifeRow label="Success rate" value={lifeSummary ? pct(lifeSummary.success_rate, 1) : null} />
                <LifeRow label="Average response" value={lifeSummary ? ms(lifeSummary.avg_response_time_ms) : null} />
              </dl>
            </Panel>
          </section>

          <Panel className="reveal" style={{ ["--d" as string]: "240ms" }}>
            {o ? <LaneChart series={o.series} height={300} /> : <Pulse className="m-6 h-72" />}
          </Panel>

          <ModelsTable rows={o?.top_models ?? []} loading={!o} delay={280} />

          <section className="reveal" style={{ ["--d" as string]: "320ms" }}>
            <Rule description="Every call made with this project's key. Open one for its tokens, cost and the reply.">
              Requests
              <span className="tnum ml-3 text-base font-semibold text-text-3">{count(logs.data?.total_count ?? 0)} logged</span>
            </Rule>
            {logs.isError ? (
              <Failed title="Could not load request logs" onRetry={() => logs.refetch()} />
            ) : logs.isLoading ? (
              <Panel className="space-y-2 p-5">
                {Array.from({ length: 6 }).map((_, i) => <Pulse key={i} className="h-10 w-full" />)}
              </Panel>
            ) : (logs.data?.logs.length ?? 0) === 0 ? (
              <Panel>
                <Empty
                  icon={CircleAlert}
                  title="Nothing logged yet"
                  body="Every call through this key is recorded with its lane, tokens and cost."
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
                  <div className="mt-5 flex justify-center">
                    <Button onClick={() => setLimit(limit + 25)} busy={logs.isFetching}>
                      Show 25 more
                    </Button>
                  </div>
                )}
              </>
            )}
          </section>
        </div>
      )}
    </div>
  );
}

function Tile({
  tint,
  label,
  value,
  foot,
  trend,
  note,
}: {
  tint: string;
  label: string;
  value: React.ReactNode;
  foot?: React.ReactNode;
  trend?: number[];
  note?: string;
}) {
  return (
    <Panel className={cn(tint, "glow flex min-h-[11rem] flex-col p-5")}>
      <p className="text-base font-semibold text-text-2">{label}</p>
      <p className="mt-2 text-3xl font-extrabold text-text">{value ?? <Pulse className="h-9 w-24" />}</p>
      {foot}
      <div className="mt-auto pt-3">
        {trend && trend.length > 1 && <Sparkline values={trend} height={34} />}
        {note && <p className="mt-2 text-sm text-text-3">{note}</p>}
      </div>
    </Panel>
  );
}

function LifeRow({ label, value }: { label: string; value: string | null }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-hair pb-4 last:border-0 last:pb-0">
      <dt className="text-base text-text-3">{label}</dt>
      <dd className="num text-xl font-extrabold text-text">{value ?? <Pulse className="h-6 w-16" />}</dd>
    </div>
  );
}

function backendForLog(l: UsageLog) {
  if (l.provider === "anthropic") return backendOf("credit");
  if (l.provider === "claude-code") return backendOf("bridge");
  if (l.model_type === "free" || l.cost_source === "free_tier") return backendOf("free");
  return backendOf(l.provider ? "openrouter" : "other");
}

function LogRow({ log }: { log: UsageLog }) {
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const lane = laneOf(log.model_type, log.model_tier);
  const backend = backendForLog(log);

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
    <li>
      <button
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        className="grid w-full grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-4 px-5 py-3.5 text-left transition-colors hover:bg-raised/60 sm:grid-cols-[auto_minmax(0,1.2fr)_minmax(0,1fr)_6rem_6rem_7rem_auto] sm:px-6"
      >
        {log.success ? (
          <span className="h-2.5 w-2.5 rounded-full bg-ok" aria-label="Succeeded" />
        ) : (
          <CircleAlert className="h-4 w-4 text-bad" aria-label="Failed" />
        )}
        <span className="min-w-0">
          <span className="block truncate text-base font-semibold text-text">{modelShortName(log.model_id)}</span>
          <span className="flex items-center gap-2 text-sm text-text-3">
            <LaneDot scope={lane.scope} className="h-2 w-2" />
            {lane.label}
          </span>
        </span>
        <span className="hidden min-w-0 items-center gap-1.5 truncate text-sm text-text-3 sm:flex">
          <backend.icon className="h-3.5 w-3.5 shrink-0" aria-hidden />
          {backend.label}
          {log.streamed && ", streamed"}
        </span>
        <span className="tnum hidden text-right text-base text-text-2 sm:block">{compact(log.total_tokens)}</span>
        <span className="tnum hidden text-right text-base text-text-2 sm:block">{ms(log.response_time_ms)}</span>
        <span className="tnum hidden text-right text-base sm:block">
          {log.credit_cost_usd != null ? (
            <span className="text-text-2">{money(Number(log.credit_cost_usd))} <span className="text-text-3">cr</span></span>
          ) : (
            <span className={Number(log.cost_usd) > 0 ? "font-semibold text-text" : "text-text-3"}>{money(Number(log.cost_usd))}</span>
          )}
        </span>
        <span className="flex items-center gap-2 text-sm text-text-3">
          {ago(log.created_at)}
          <ChevronDown className={cn("h-4 w-4 transition-transform duration-300", open && "rotate-180")} aria-hidden />
        </span>
      </button>

      {open && (
        <div className="space-y-5 border-t border-hair bg-sunken/70 px-6 py-5 animate-lift">
          <dl className="grid grid-cols-2 gap-5 md:grid-cols-4">
            <Detail label="Answered by" value={`${backend.label}${backend.billed ? ", billed" : ""}`} />
            <Detail label="Model" value={log.model_id} />
            <Detail label="Tokens" value={`${count(log.input_tokens)} in, ${count(log.output_tokens)} out`} />
            <Detail label="First token" value={log.streamed ? ms(log.time_to_first_token_ms) : "Not streamed"} />
            <Detail
              label="Cost"
              value={
                log.credit_cost_usd != null
                  ? `${money(Number(log.credit_cost_usd))} of credit`
                  : Number(log.cost_usd) > 0
                    ? `${money(Number(log.cost_usd))} billed`
                    : "Free"
              }
            />
            {log.avoided_cost_usd != null && Number(log.avoided_cost_usd) > 0 && (
              <Detail label="A paid model would have charged" value={money(Number(log.avoided_cost_usd))} />
            )}
            <Detail label="When" value={dateTime(log.created_at)} />
            <Detail label="Request ID" value={log.request_id || "Not recorded"} />
          </dl>

          {log.error_message && (
            <div className={cn("rounded-xl px-4 py-3", log.success ? "bg-panel ring-1 ring-hair" : "bg-bad/[0.08] ring-1 ring-bad/30")}>
              <p className="text-base text-text-2">{log.error_message}</p>
            </div>
          )}

          {log.response_content && (
            <div>
              <div className="mb-2 flex items-center justify-between">
                <p className="text-base font-semibold text-text-2">Reply</p>
                <Button size="sm" variant="ghost" onClick={copy}>
                  {copied ? <Check className="h-4 w-4 text-ok" aria-hidden /> : <Copy className="h-4 w-4" aria-hidden />}
                  {copied ? "Copied" : "Copy"}
                </Button>
              </div>
              <div className="max-h-56 overflow-y-auto rounded-xl border border-hair bg-panel px-4 py-3.5">
                <p className="whitespace-pre-wrap text-base leading-relaxed text-text-2">
                  {log.response_content.length > 1200 ? `${log.response_content.slice(0, 1200)}…` : log.response_content}
                </p>
              </div>
            </div>
          )}
        </div>
      )}
    </li>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-sm text-text-3">{label}</dt>
      <dd className="mt-1 break-words text-base font-semibold text-text">{value}</dd>
    </div>
  );
}
