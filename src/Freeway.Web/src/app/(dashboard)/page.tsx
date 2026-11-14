"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, ChevronDown, CircleAlert, Info } from "lucide-react";
import { LiveFeed, ModelsTable } from "@/components/overview/sections";
import { Panel, PanelHead, Rule } from "@/components/ui/panel";
import { Segmented } from "@/components/ui/segmented";
import { Pulse } from "@/components/ui/pulse";
import { Failed } from "@/components/ui/state";
import { Tag } from "@/components/ui/tag";
import { AnimatedNumber, Delta, LaneDot, Meter, Ring, Sparkline } from "@/components/charts/primitives";
import { LaneChart } from "@/components/charts/lane-chart";
import { TrafficMap } from "@/components/charts/traffic-map";
import { analyticsApi } from "@/lib/api/analytics";
import { creditsApi } from "@/lib/api/credits";
import { laneOf } from "@/lib/theme/lanes";
import { BACKEND_ORDER, backendOf } from "@/lib/theme/backends";
import { cn } from "@/lib/utils/cn";
import { cents, count, money, ms, pct } from "@/lib/utils/format";
import type { Overview } from "@/lib/types";

type Window = "7" | "30" | "90";

export default function OverviewPage() {
  const [days, setDays] = useState<Window>("30");

  const q = useQuery({
    queryKey: ["overview", days],
    queryFn: () => analyticsApi.getOverview(Number(days)),
    refetchInterval: 30_000,
    placeholderData: (prev) => prev,
  });

  const o = q.data;

  return (
    <div className="space-y-10">
      <Headline o={o} loading={q.isLoading} days={Number(days)} />

      <div className="reveal flex flex-wrap items-center gap-4" style={{ ["--d" as string]: "80ms" }}>
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
        <span className="flex items-center gap-2 text-sm text-text-3">
          <span className="live-dot h-2 w-2 rounded-full bg-ok text-ok" aria-hidden />
          Live, refreshing every 30 seconds
        </span>
      </div>

      {q.isError ? (
        <Failed
          title="Could not load the overview"
          body="The gateway did not return its figures. Check that the API is reachable."
          onRetry={() => q.refetch()}
        />
      ) : (
        <div className={cn("space-y-10 transition-opacity duration-300", q.isFetching && q.isPlaceholderData && "opacity-60")}>
          <Attention />
          <Kpis o={o} />

          <Panel className="reveal" style={{ ["--d" as string]: "200ms" }}>
            {o ? <TrafficMap flows={o.flows} days={o.days} /> : <Pulse className="m-6 h-[28rem]" />}
          </Panel>

          <section className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_25rem]">
            <Panel className="reveal" style={{ ["--d" as string]: "260ms" }}>
              {o ? <LaneChart series={o.series} height={360} /> : <Pulse className="m-6 h-80" />}
            </Panel>
            <Routes o={o} />
          </section>

          <section className="grid gap-6 2xl:grid-cols-2">
            <ProjectsTable o={o} />
            <ModelsTable rows={o?.top_models ?? []} loading={!o} />
          </section>

          <LiveFeed recent={o?.recent ?? []} loading={!o} />
        </div>
      )}
    </div>
  );
}

/**
 * The page's opening line: what happened, in a sentence, with the figures in it.
 * Read once, it answers the questions this console is opened to ask.
 */
function Headline({ o, loading, days }: { o?: Overview; loading: boolean; days: number }) {
  if (loading || !o) {
    return (
      <div className="space-y-3">
        <Pulse className="h-12 w-full max-w-3xl" />
        <Pulse className="h-12 w-2/3 max-w-2xl" />
      </div>
    );
  }
  const w = o.window;
  const freeShare = w.requests ? Math.round((w.unbilled / w.requests) * 100) : 0;

  return (
    <header className="reveal max-w-5xl">
      <p className="text-base font-semibold text-text-3">Overview, last {days} days</p>
      <h1 className="mt-3 text-3xl font-extrabold leading-tight text-text sm:text-4xl xl:text-[3.25rem] xl:leading-[1.08]">
        <span className="text-grad">{count(w.requests)} requests, {freeShare}% answered without a bill.</span>{" "}
        <span className="text-text-2">
          You paid {cents(Number(w.cost))}
          {Number(w.avoided_usd) > 0 && <> and saved about {cents(Number(w.avoided_usd))}</>}.
        </span>
      </h1>
    </header>
  );
}

function Kpis({ o }: { o?: Overview }) {
  const credit = useQuery({ queryKey: ["credits"], queryFn: creditsApi.getOverview, refetchInterval: 60_000 });
  const w = o?.window;
  const p = o?.previous_window;
  const series = o?.series ?? [];
  const c = credit.data;

  const cells = [
    {
      tint: "tint-coral",
      label: "Billed",
      value: w ? <AnimatedNumber value={Number(w.cost)} format={cents} /> : null,
      foot: w && p ? <Delta current={Number(w.cost)} previous={Number(p.cost)} upIsGood={false} /> : null,
      trend: series.map((s) => Number(s.cost)),
      note: "Charged by OpenRouter. Everything else was free.",
    },
    {
      tint: "tint-sky",
      label: "Requests",
      value: w ? <AnimatedNumber value={w.requests} format={(v) => count(Math.round(v))} /> : null,
      foot: w && p ? <Delta current={w.requests} previous={p.requests} /> : null,
      trend: series.map((s) => s.requests),
      note: w ? `${pct(w.requests ? ((w.requests - w.failures) / w.requests) * 100 : 100, 1)} succeeded` : "",
    },
    {
      tint: "tint-mint",
      label: "Answered free",
      value: w ? <AnimatedNumber value={w.requests ? (w.unbilled / w.requests) * 100 : 0} format={(v) => `${v.toFixed(0)}%`} /> : null,
      foot: w ? <Meter value={w.requests ? (w.unbilled / w.requests) * 100 : 0} warnAt={101} badAt={101} label="Share answered without a bill" className="mt-2" /> : null,
      note: w ? `${count(w.unbilled)} by the credit, Claude Code or free providers` : "",
    },
    {
      tint: "tint-amber",
      label: "Saved",
      value: w ? <AnimatedNumber value={Number(w.avoided_usd)} format={cents} /> : null,
      trend: series.map((s) => Number(s.avoided_usd ?? 0)),
      note: "What paid models would have charged for the free answers",
    },
    {
      tint: "tint-violet",
      label: "Response time",
      value: w ? <AnimatedNumber value={w.p50_response_ms} format={(v) => ms(v)} /> : null,
      note: w ? `Median. 95% finish within ${ms(w.p95_response_ms)}` : "",
    },
  ];

  return (
    <section className="reveal grid gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6" style={{ ["--d" as string]: "140ms" }} aria-label="Key figures">
      <Panel className="tint-pink glow flex flex-col justify-between p-5 sm:col-span-2 lg:col-span-1 lg:row-span-2 2xl:row-span-1">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-base font-semibold text-text-2">Claude credit</p>
            <p className="mt-1 text-sm text-text-3">
              {c?.grant ? `Resets in ${Math.ceil(c.days_left)} days` : c?.configured === false ? "Not set up" : " "}
            </p>
          </div>
          <Link href="/credits" className="text-sm font-semibold text-text-2 underline-offset-4 hover:text-text hover:underline">
            Details
          </Link>
        </div>
        <div className="mt-4 flex items-center gap-4">
          <Ring value={c?.used_percent ?? 0} label="Claude credit used" size={84} stroke={8} warnAt={80} badAt={95}>
            <span>
              <span className="num block text-lg font-extrabold text-text">{c ? `${Math.round(c.used_percent)}%` : "–"}</span>
              <span className="block text-2xs text-text-3">used</span>
            </span>
          </Ring>
          <div className="min-w-0">
            <p className="num text-2xl font-extrabold text-text">
              {c ? <AnimatedNumber value={c.remaining_usd} format={cents} /> : <Pulse className="h-7 w-20" />}
            </p>
            <p className="text-sm text-text-3">left of {c?.grant ? cents(c.grant.amount_usd) : "the grant"}</p>
          </div>
        </div>
      </Panel>

      {cells.map((cell) => (
        <Panel key={cell.label} className={cn(cell.tint, "glow flex min-h-[11rem] flex-col p-5")}>
          <p className="text-base font-semibold text-text-2">{cell.label}</p>
          <p className="mt-2 text-3xl font-extrabold text-text">{cell.value ?? <Pulse className="h-9 w-24" />}</p>
          {cell.foot}
          <div className="mt-auto pt-3">
            {cell.trend && cell.trend.length > 1 && <Sparkline values={cell.trend} height={34} />}
            {cell.note && <p className="mt-2 text-sm text-text-3">{cell.note}</p>}
          </div>
        </Panel>
      ))}
    </section>
  );
}

/** Things that need doing, from the same checks that send alert emails. Quiet when there are none. */
function Attention() {
  const alerts = useQuery({ queryKey: ["alerts"], queryFn: analyticsApi.getAlerts, staleTime: 10 * 60_000 });
  const [open, setOpen] = useState(false);
  const list = alerts.data?.alerts ?? [];
  if (list.length === 0) return null;

  const critical = list.filter((a) => a.severity === 2).length;

  return (
    <section className={cn("reveal rounded-panel border px-5 py-4", critical ? "border-bad/35 bg-bad/[0.06]" : "border-warn/35 bg-warn/[0.06]")}>
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex w-full items-center gap-3 text-left">
        <AlertTriangle className={cn("h-5 w-5 shrink-0", critical ? "text-bad" : "text-warn")} aria-hidden />
        <span className="flex-1 text-base font-bold text-text">
          {list.length === 1 ? "1 thing needs attention" : `${list.length} things need attention`}
          <span className="ml-2 font-normal text-text-2">{list[0].title}{list.length > 1 ? " and more" : ""}</span>
        </span>
        <ChevronDown className={cn("h-5 w-5 text-text-3 transition-transform duration-300", open && "rotate-180")} aria-hidden />
      </button>
      {open && (
        <ul className="mt-4 space-y-3 animate-lift">
          {list.map((a) => (
            <li key={a.key} className="flex gap-3 rounded-xl bg-panel/70 p-4 ring-1 ring-hair">
              {a.severity === 2 ? (
                <CircleAlert className="mt-0.5 h-4 w-4 shrink-0 text-bad" aria-label="Critical" />
              ) : a.severity === 1 ? (
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-warn" aria-label="Warning" />
              ) : (
                <Info className="mt-0.5 h-4 w-4 shrink-0 text-text-2" aria-label="Information" />
              )}
              <div className="min-w-0">
                <p className="text-base font-semibold text-text">{a.title}</p>
                <p className="mt-0.5 text-sm text-text-2">{a.detail}</p>
                {a.action && <p className="mt-1.5 text-sm text-text-3">{a.action}</p>}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** The three routes, right now, and how much of the window each answered. */
function Routes({ o }: { o?: Overview }) {
  const bridge = useQuery({ queryKey: ["local-claude"], queryFn: analyticsApi.getLocalClaude, refetchInterval: 60_000 });
  const credit = useQuery({ queryKey: ["credits"], queryFn: creditsApi.getOverview, refetchInterval: 60_000 });
  const total = o?.window.requests ?? 0;
  const byBackend = new Map((o?.backends ?? []).map((b) => [b.backend, b]));
  const b = bridge.data;
  const c = credit.data;

  const plan = b?.plan?.windows ?? {};
  const fiveHour = plan["five_hour"]?.utilization_percent;
  const sevenDay = plan["seven_day"]?.utilization_percent;

  const status = (key: string) => {
    if (key === "credit") return !c ? null : !c.configured ? ["Off", "neutral"] : c.can_serve ? ["Available", "ok"] : ["Paused", "bad"];
    if (key === "bridge") return !b ? null : !b.configured ? ["Off", "neutral"] : b.can_serve ? ["Available", "ok"] : [b.state === "ratelimited" ? "Rate limited" : "Unavailable", "bad"];
    if (key === "openrouter") return ["Standby", "neutral"];
    return ["Always on", "ok"];
  };

  return (
    <Panel className="reveal flex flex-col" style={{ ["--d" as string]: "300ms" }}>
      <PanelHead title="Who answered" meta="Each route's share of the window, and whether it can take traffic now" />
      <ul className="mt-2 flex-1 divide-y divide-hair px-5 pb-2 sm:px-6">
        {BACKEND_ORDER.filter((k) => k !== "other" || byBackend.has("other")).map((k) => {
          const meta = backendOf(k);
          const u = byBackend.get(k);
          const share = total && u ? (u.requests / total) * 100 : 0;
          const s = status(k);
          return (
            <li key={k} className="py-4">
              <div className="flex items-center gap-3">
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-sunken text-text-2 ring-1 ring-hair">
                  <meta.icon className="h-4 w-4" aria-hidden />
                </span>
                <div className="min-w-0 flex-1">
                  <p className="flex items-center gap-2 text-base font-bold text-text">
                    {meta.label}
                    {meta.billed && <Tag tone="warn">billed</Tag>}
                  </p>
                  <p className="text-sm text-text-3">
                    {u ? `${count(u.requests)} requests, ${ms(u.avg_response_ms)} average` : "No requests in this window"}
                  </p>
                </div>
                <div className="text-right">
                  <p className="num text-xl font-extrabold text-text">{pct(share)}</p>
                  {s && (
                    <p className="flex items-center justify-end gap-1.5 text-xs text-text-3">
                      <span className={cn("h-1.5 w-1.5 rounded-full", s[1] === "ok" ? "bg-ok" : s[1] === "bad" ? "bg-bad" : "bg-text-3")} aria-hidden />
                      {s[0]}
                    </p>
                  )}
                </div>
              </div>
              <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-hair-bright/60">
                <div
                  className={cn("h-full rounded-full transition-[width] duration-1000 ease-out", meta.billed ? "bg-warn" : "bg-text-2")}
                  style={{ width: `${share}%` }}
                />
              </div>
              {k === "bridge" && b?.configured && (fiveHour != null || sevenDay != null) && (
                <div className="mt-3 grid grid-cols-2 gap-4">
                  {fiveHour != null && (
                    <div>
                      <p className="mb-1.5 flex justify-between text-xs text-text-3"><span>Plan, 5 hours</span><span className="tnum font-semibold text-text-2">{Math.round(fiveHour)}%</span></p>
                      <Meter value={fiveHour} label="Claude plan, five-hour window" />
                    </div>
                  )}
                  {sevenDay != null && (
                    <div>
                      <p className="mb-1.5 flex justify-between text-xs text-text-3"><span>Plan, 7 days</span><span className="tnum font-semibold text-text-2">{Math.round(sevenDay)}%</span></p>
                      <Meter value={sevenDay} label="Claude plan, seven-day window" />
                    </div>
                  )}
                </div>
              )}
              {k === "credit" && u && (
                <p className="mt-2 text-sm text-text-3">{cents(Number(u.credit_usd))} of credit used</p>
              )}
              {k === "openrouter" && u && (
                <p className="mt-2 text-sm text-text-3">{cents(Number(u.cost))} billed</p>
              )}
            </li>
          );
        })}
      </ul>
    </Panel>
  );
}

function ProjectsTable({ o }: { o?: Overview }) {
  const rows = o?.top_projects ?? [];
  return (
    <Panel className="reveal" style={{ ["--d" as string]: "340ms" }}>
      <PanelHead
        title="Projects"
        meta="Busiest first"
        action={<Link href="/projects" className="text-sm font-semibold text-text-2 underline-offset-4 hover:text-text hover:underline">All projects</Link>}
      />
      <div className="mt-3 overflow-x-auto">
        <table className="w-full min-w-[36rem] text-left">
          <thead>
            <tr className="border-y border-hair text-sm text-text-3">
              <th className="px-6 py-2.5 font-semibold">Project</th>
              <th className="px-3 py-2.5 font-semibold">Trend</th>
              <th className="px-3 py-2.5 text-right font-semibold">Requests</th>
              <th className="px-3 py-2.5 text-right font-semibold">Credit</th>
              <th className="px-6 py-2.5 text-right font-semibold">Billed</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-hair">
            {!o
              ? Array.from({ length: 4 }).map((_, i) => (
                  <tr key={i}><td colSpan={5} className="px-6 py-3"><Pulse className="h-6 w-full" /></td></tr>
                ))
              : rows.length === 0
                ? <tr><td colSpan={5} className="px-6 py-10 text-center text-base text-text-3">No traffic in this window.</td></tr>
                : rows.map((p) => {
                    const lane = laneOf("paid", p.top_lane);
                    return (
                      <tr key={p.project_id} className="group transition-colors hover:bg-raised/60">
                        <td className="px-6 py-3">
                          <Link href={`/projects/${p.project_id}`} className="flex items-center gap-3">
                            <LaneDot scope={lane.scope} />
                            <span className="min-w-0">
                              <span className="block truncate text-base font-semibold text-text group-hover:underline">{p.name}</span>
                              <span className="block text-sm text-text-3">Mostly {lane.label.toLowerCase()}, {ms(p.avg_response_ms)} average</span>
                            </span>
                          </Link>
                        </td>
                        <td className="w-32 px-3 py-3"><Sparkline values={p.daily ?? []} height={28} /></td>
                        <td className="tnum px-3 py-3 text-right text-base font-semibold text-text">{count(p.requests)}</td>
                        <td className="tnum px-3 py-3 text-right text-base text-text-2">{money(Number(p.credit_usd ?? 0))}</td>
                        <td className="tnum px-6 py-3 text-right text-base text-text-2">{money(Number(p.cost))}</td>
                      </tr>
                    );
                  })}
          </tbody>
        </table>
      </div>
    </Panel>
  );
}

