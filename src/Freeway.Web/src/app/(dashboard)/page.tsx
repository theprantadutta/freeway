"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Activity, ArrowRight, CircleAlert } from "lucide-react";
import { PageHead } from "@/components/shell/page-head";
import { Panel, PanelHead, Rule } from "@/components/ui/panel";
import { Tag } from "@/components/ui/tag";
import { Pulse } from "@/components/ui/pulse";
import { Failed, Empty } from "@/components/ui/state";
import { Segmented } from "@/components/ui/segmented";
import { SeriesChart } from "@/components/chart/series-chart";
import { LaneSplit } from "@/components/chart/lane-split";
import { analyticsApi } from "@/lib/api/analytics";
import { modelsApi } from "@/lib/api/models";
import { LANES, laneOf, laneLabel } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";
import {
  ago,
  change,
  compact,
  count,
  currentMonthLabel,
  modelShortName,
  money,
  ms,
  pct,
} from "@/lib/utils/format";

type Window = "7" | "30" | "90";

export default function OverviewPage() {
  const [days, setDays] = useState<Window>("30");

  const q = useQuery({
    queryKey: ["overview", days],
    queryFn: () => analyticsApi.getOverview(Number(days)),
  });

  const o = q.data;
  const t = o?.totals;
  const costDelta = t ? change(Number(t.cost_this_month), Number(t.cost_previous_month)) : null;
  const reqDelta = t ? change(t.requests_this_month, t.requests_previous_month) : null;

  return (
    <div className="space-y-7">
      <PageHead
        title="Overview"
        lede={`What moved through the gateway. Figures cover ${currentMonthLabel()} unless a window says otherwise.`}
        actions={
          <Segmented
            label="Time window"
            value={days}
            onChange={setDays}
            segments={[
              { value: "7", label: "7d" },
              { value: "30", label: "30d" },
              { value: "90", label: "90d" },
            ]}
          />
        }
      />

      {q.isError ? (
        <Failed
          title="Could not load the console"
          body="The gateway did not return its overview. Check that the API is reachable."
          onRetry={() => q.refetch()}
        />
      ) : (
        <>
          {/* The month's spend is the figure this page is about, so it gets the
              display size and the chart sits beside it rather than under a row of
              equal-weight tiles. */}
          <section className="enter grid gap-4 lg:grid-cols-[minmax(0,22rem)_minmax(0,1fr)]">
            <Panel className="lane-premium lane-halo flex flex-col justify-between p-5">
              <div>
                <p className="text-2xs font-medium uppercase text-text-3">
                  Spent in {currentMonthLabel()}
                </p>
                {q.isLoading ? (
                  <Pulse className="mt-3 h-14 w-44" />
                ) : (
                  <p className="fig-hero mt-3 text-5xl text-text">
                    {money(Number(t?.cost_this_month ?? 0))}
                  </p>
                )}
                <Delta value={costDelta} suffix="vs last month" />
              </div>

              <dl className="mt-6 grid grid-cols-2 gap-x-4 gap-y-3 border-t border-hair pt-4">
                <Figure label="Requests" value={compact(t?.requests_this_month)} loading={q.isLoading} />
                <Figure
                  label="Delta"
                  value={reqDelta == null ? "—" : `${reqDelta > 0 ? "+" : ""}${reqDelta.toFixed(0)}%`}
                  loading={q.isLoading}
                />
                <Figure label="Tokens" value={compact(t?.tokens_this_month)} loading={q.isLoading} />
                <Figure
                  label="Success"
                  value={pct(t?.success_rate_this_month, 1)}
                  loading={q.isLoading}
                  tone={t && t.success_rate_this_month < 95 ? "bad" : undefined}
                />
                <Figure label="Avg latency" value={ms(t?.avg_response_ms_this_month)} loading={q.isLoading} />
                <Figure label="All time" value={money(Number(t?.cost_all_time ?? 0))} loading={q.isLoading} />
              </dl>
            </Panel>

            <Panel className="flex min-h-[20rem] flex-col">
              {q.isLoading ? (
                <div className="flex flex-1 items-end gap-px p-4">
                  {Array.from({ length: 30 }).map((_, i) => (
                    <Pulse key={i} className="flex-1" style={{ height: `${20 + ((i * 37) % 70)}%` }} />
                  ))}
                </div>
              ) : (
                <SeriesChart points={o?.series ?? []} className="flex-1 pb-4" />
              )}
            </Panel>
          </section>

          <section>
            <Rule>Where it went · last {days} days</Rule>
            <div className="enter grid gap-4 lg:grid-cols-3">
              <Panel>
                <PanelHead title="By lane" meta="Share of requests" />
                {q.isLoading ? (
                  <div className="space-y-3 p-4">
                    <Pulse className="h-2.5 w-full rounded-full" />
                    <Pulse className="h-16 w-full" />
                  </div>
                ) : (
                  <LaneSplit lanes={o?.lanes ?? []} />
                )}
              </Panel>

              <Panel>
                <PanelHead title="Busiest models" meta="By request count" />
                {q.isLoading ? (
                  <div className="space-y-2 p-4">
                    {Array.from({ length: 4 }).map((_, i) => (
                      <Pulse key={i} className="h-7 w-full" />
                    ))}
                  </div>
                ) : (o?.top_models.length ?? 0) === 0 ? (
                  <Empty
                    className="lane-accent py-10"
                    icon={Activity}
                    title="No traffic yet"
                    body="Send a request through the gateway and it will appear here."
                  />
                ) : (
                  <ul className="divide-y divide-hair">
                    {o!.top_models.map((m) => {
                      const lane = laneOf(m.model_type, m.model_tier);
                      const max = Math.max(...o!.top_models.map((x) => x.requests), 1);
                      return (
                        <li key={`${m.model_id}-${m.model_tier ?? ""}`} className={cn(lane.scope, "px-4 py-2.5")}>
                          <div className="flex items-baseline justify-between gap-3">
                            <span className="truncate font-mono text-xs text-text">
                              {modelShortName(m.model_id)}
                            </span>
                            <span className="fig shrink-0 text-xs text-text-2">
                              {compact(m.requests)}
                            </span>
                          </div>
                          <div className="mt-1.5 h-1 w-full overflow-hidden rounded-full bg-sunken">
                            <div
                              className="h-full rounded-full lane-bg transition-[width] duration-500 ease-out"
                              style={{ width: `${Math.max((m.requests / max) * 100, 2)}%` }}
                            />
                          </div>
                          <div className="mt-1 flex items-center justify-between">
                            <span className="text-2xs lane-fg">{lane.label}</span>
                            <span className="fig text-2xs text-text-3">{money(Number(m.cost))}</span>
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                )}
              </Panel>

              <Panel>
                <PanelHead
                  title="Busiest projects"
                  meta="By request count"
                  action={
                    <Link
                      href="/projects"
                      className="inline-flex items-center gap-1 rounded text-xs font-medium text-accent hover:underline"
                    >
                      All
                      <ArrowRight className="h-3 w-3" aria-hidden />
                    </Link>
                  }
                />
                {q.isLoading ? (
                  <div className="space-y-2 p-4">
                    {Array.from({ length: 4 }).map((_, i) => (
                      <Pulse key={i} className="h-7 w-full" />
                    ))}
                  </div>
                ) : (o?.top_projects.length ?? 0) === 0 ? (
                  <Empty
                    className="lane-accent py-10"
                    icon={Activity}
                    title="No project traffic"
                    body="Traffic attributed to a project key shows up here."
                  />
                ) : (
                  <ul className="divide-y divide-hair">
                    {o!.top_projects.map((p) => (
                      <li key={p.project_id}>
                        <Link
                          href={`/projects/${p.project_id}`}
                          className="flex items-center gap-3 px-4 py-3 transition-colors hover:bg-raised"
                        >
                          <span
                            className={cn(
                              "h-1.5 w-1.5 shrink-0 rounded-full",
                              p.is_active ? "bg-ok" : "bg-text-3"
                            )}
                            aria-hidden
                          />
                          <span className="min-w-0 flex-1 truncate text-sm text-text">{p.name}</span>
                          <span className="fig shrink-0 text-xs text-text-2">
                            {compact(p.requests)}
                          </span>
                          <span className="fig w-20 shrink-0 text-right text-xs text-text-3">
                            {money(Number(p.cost))}
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                )}
              </Panel>
            </div>
          </section>

          <section>
            <Rule>Live traffic</Rule>
            <Panel>
              {q.isLoading ? (
                <div className="divide-y divide-hair">
                  {Array.from({ length: 6 }).map((_, i) => (
                    <div key={i} className="flex items-center gap-3 px-4 py-2.5">
                      <Pulse className="h-4 w-4 shrink-0 rounded-full" />
                      <Pulse className="h-3 flex-1" />
                      <Pulse className="h-3 w-20 shrink-0" />
                    </div>
                  ))}
                </div>
              ) : (o?.recent.length ?? 0) === 0 ? (
                <Empty
                  className="lane-accent"
                  icon={Activity}
                  title="Nothing has come through yet"
                  body="Every request is logged here with its lane, latency and cost."
                />
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[42rem] text-sm">
                    <thead>
                      <tr className="border-b border-hair text-left">
                        {["", "Project", "Model", "Lane", "Tokens", "Latency", "Cost", "When"].map(
                          (h, i) => (
                            <th
                              key={i}
                              className={cn(
                                "px-3 py-2 text-2xs font-medium uppercase text-text-3",
                                i >= 4 && "text-right"
                              )}
                            >
                              {h}
                            </th>
                          )
                        )}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-hair">
                      {o!.recent.map((r) => {
                        const lane = laneOf(r.model_type, r.model_tier);
                        return (
                          <tr key={r.id} className={cn(lane.scope, "transition-colors hover:bg-raised")}>
                            <td className="w-8 px-3 py-2.5">
                              {r.success ? (
                                <span className="block h-1.5 w-1.5 rounded-full bg-ok" aria-label="ok" />
                              ) : (
                                <CircleAlert className="h-3.5 w-3.5 text-bad" aria-label="failed" />
                              )}
                            </td>
                            <td className="max-w-[10rem] truncate px-3 py-2.5 text-text-2">
                              {r.project_name}
                            </td>
                            <td className="max-w-[16rem] truncate px-3 py-2.5 font-mono text-xs text-text">
                              {modelShortName(r.model_id)}
                            </td>
                            <td className="px-3 py-2.5">
                              <Tag tone="lane" mono>
                                {laneLabel(r.model_type, r.model_tier)}
                              </Tag>
                            </td>
                            <td className="fig px-3 py-2.5 text-right text-xs text-text-2">
                              {compact(r.total_tokens)}
                            </td>
                            <td className="fig px-3 py-2.5 text-right text-xs text-text-2">
                              {ms(r.response_time_ms)}
                            </td>
                            <td className="fig px-3 py-2.5 text-right text-xs text-text">
                              {money(Number(r.cost_usd))}
                            </td>
                            <td className="px-3 py-2.5 text-right text-xs text-text-3">
                              {ago(r.created_at)}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </Panel>
          </section>

          <section>
            <Rule
              action={
                <Link
                  href="/models"
                  className="inline-flex items-center gap-1 rounded text-xs font-medium text-accent hover:underline"
                >
                  Change routing
                  <ArrowRight className="h-3 w-3" aria-hidden />
                </Link>
              }
            >
              Routing table
            </Rule>
            <LaneRouting />
          </section>
        </>
      )}
    </div>
  );
}

function Figure({
  label,
  value,
  loading,
  tone,
}: {
  label: string;
  value: string;
  loading?: boolean;
  tone?: "bad";
}) {
  return (
    <div>
      <dt className="text-2xs uppercase text-text-3">{label}</dt>
      <dd className={cn("fig mt-0.5 text-sm", tone === "bad" ? "text-bad" : "text-text")}>
        {loading ? <Pulse className="h-4 w-14" /> : value}
      </dd>
    </div>
  );
}

function Delta({ value, suffix }: { value: number | null; suffix: string }) {
  if (value == null) {
    return <p className="mt-2 text-xs text-text-3">no prior month to compare</p>;
  }
  const up = value > 0;
  return (
    <p className="mt-2 text-xs">
      <span className={cn("fig font-medium", up ? "text-bad" : "text-ok")}>
        {up ? "▲" : "▼"} {Math.abs(value).toFixed(0)}%
      </span>
      <span className="ml-1.5 text-text-3">{suffix}</span>
    </p>
  );
}

/** Which model each lane currently routes to. Reference, so it sits at the bottom. */
function LaneRouting() {
  const free = useQuery({ queryKey: ["model", "free"], queryFn: () => modelsApi.getSelectedFreeModel() });
  const low = useQuery({ queryKey: ["model", "paid", "low"], queryFn: () => modelsApi.getSelectedPaidModelForTier("low") });
  const moderate = useQuery({ queryKey: ["model", "paid", "moderate"], queryFn: () => modelsApi.getSelectedPaidModelForTier("moderate") });
  const premium = useQuery({ queryKey: ["model", "paid", "premium"], queryFn: () => modelsApi.getSelectedPaidModelForTier("premium") });
  const image = useQuery({ queryKey: ["model", "image"], queryFn: () => modelsApi.getSelectedImageModel() });

  const rows = [
    { lane: LANES.free, q: free },
    { lane: LANES.low, q: low },
    { lane: LANES.moderate, q: moderate },
    { lane: LANES.premium, q: premium },
    { lane: LANES.image, q: image },
  ];

  return (
    <Panel>
      <ul className="divide-y divide-hair">
        {rows.map(({ lane, q }) => (
          <li
            key={lane.key}
            className={cn(lane.scope, "flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3")}
          >
            <span className="flex w-28 shrink-0 items-center gap-2">
              <span className="h-2 w-2 rounded-full lane-bg" aria-hidden />
              <span className="text-sm font-medium text-text">{lane.label}</span>
            </span>
            <code className="shrink-0 rounded bg-sunken px-1.5 py-0.5 font-mono text-2xs lane-fg">
              {lane.model}
            </code>
            <span className="min-w-0 flex-1 truncate font-mono text-xs text-text-2">
              {q.isLoading ? <Pulse className="h-3 w-48" /> : q.data?.model_id ?? "not selected"}
            </span>
            {q.data?.pricing && (
              <span className="fig shrink-0 text-2xs text-text-3">
                {money(parseFloat(q.data.pricing.prompt) * 1_000_000)} in ·{" "}
                {money(parseFloat(q.data.pricing.completion) * 1_000_000)} out /M
              </span>
            )}
          </li>
        ))}
      </ul>
    </Panel>
  );
}
