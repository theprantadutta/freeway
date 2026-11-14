"use client";

import { useMemo, useState } from "react";
import { ChevronDown, CircleAlert, Radio } from "lucide-react";
import { Panel, PanelHead, Rule } from "@/components/ui/panel";
import { Segmented } from "@/components/ui/segmented";
import { Pulse } from "@/components/ui/pulse";
import { LaneDot } from "@/components/charts/primitives";
import { useArrivals } from "@/lib/hooks/motion";
import { laneOf } from "@/lib/theme/lanes";
import { backendOf } from "@/lib/theme/backends";
import { cn } from "@/lib/utils/cn";
import { ago, compact, count, modelShortName, money, ms } from "@/lib/utils/format";
import type { RecentRequest, TopModel } from "@/lib/types";

export function ModelsTable({ rows, loading = false, delay = 380 }: { rows: TopModel[]; loading?: boolean; delay?: number }) {
  const max = Math.max(...rows.map((m) => m.requests), 1);
  return (
    <Panel className="reveal" style={{ ["--d" as string]: `${delay}ms` }}>
      <PanelHead title="Models" meta="What actually answered, busiest first" />
      <div className="mt-3 overflow-x-auto">
        <table className="w-full min-w-[36rem] text-left">
          <thead>
            <tr className="border-y border-hair text-sm text-text-3">
              <th className="px-6 py-2.5 font-semibold">Model</th>
              <th className="px-3 py-2.5 font-semibold">Requests</th>
              <th className="px-3 py-2.5 text-right font-semibold">Avg</th>
              <th className="px-6 py-2.5 text-right font-semibold">Cost</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-hair">
            {loading
              ? Array.from({ length: 4 }).map((_, i) => (
                  <tr key={i}><td colSpan={4} className="px-6 py-3"><Pulse className="h-6 w-full" /></td></tr>
                ))
              : rows.map((m) => {
                  const lane = laneOf(m.model_type, m.model_tier);
                  const backend = backendOf(m.backend);
                  return (
                    <tr key={`${m.model_id}-${m.model_tier ?? ""}`} className="transition-colors hover:bg-raised/60">
                      <td className="px-6 py-3">
                        <span className="block truncate text-base font-semibold text-text">{modelShortName(m.model_id)}</span>
                        <span className="flex items-center gap-2 text-sm text-text-3">
                          <LaneDot scope={lane.scope} className="h-2 w-2" />
                          {lane.label}, mostly via {backend.label}
                        </span>
                      </td>
                      <td className="w-44 px-3 py-3">
                        <span className="tnum text-base font-semibold text-text">{count(m.requests)}</span>
                        <span className={cn(lane.scope, "mt-1.5 block h-1.5 rounded-full lane-bg grow-x")} style={{ width: `${Math.max((m.requests / max) * 100, 3)}%` }} />
                      </td>
                      <td className="tnum px-3 py-3 text-right text-base text-text-2">{ms(m.avg_response_ms)}</td>
                      <td className="tnum px-6 py-3 text-right text-base text-text-2">
                        {Number(m.credit_usd ?? 0) > 0 ? `${money(Number(m.credit_usd))} credit` : money(Number(m.cost))}
                      </td>
                    </tr>
                  );
                })}
          </tbody>
        </table>
      </div>
    </Panel>
  );
}

type FeedFilter = "all" | "failed" | "billed" | "streamed";

/** The latest requests, as they arrive. New ones slide in and glow for a moment. */
export function LiveFeed({
  recent,
  loading = false,
  description = "The most recent requests across every project. Open one to see what it cost and how it was served.",
}: {
  recent: RecentRequest[];
  loading?: boolean;
  description?: string;
}) {
  const [filter, setFilter] = useState<FeedFilter>("all");
  const [open, setOpen] = useState<string | null>(null);
  const all = recent;
  const fresh = useArrivals(all.map((r) => r.id));

  const rows = useMemo(
    () =>
      all.filter((r) =>
        filter === "all" ? true : filter === "failed" ? !r.success : filter === "billed" ? backendOf(r.backend).billed : r.streamed
      ),
    [all, filter]
  );

  return (
    <section className="reveal" style={{ ["--d" as string]: "420ms" }}>
      <Rule
        description={description}
        action={
          <Segmented
            size="sm"
            label="Show"
            value={filter}
            onChange={setFilter}
            segments={[
              { value: "all", label: "All" },
              { value: "failed", label: "Failed" },
              { value: "billed", label: "Billed" },
              { value: "streamed", label: "Streamed" },
            ]}
          />
        }
      >
        <span className="inline-flex items-center gap-2.5">
          <Radio className="h-5 w-5 text-text-2" aria-hidden />
          Live requests
        </span>
      </Rule>
      <Panel>
        {loading ? (
          <div className="space-y-2 p-5">{Array.from({ length: 6 }).map((_, i) => <Pulse key={i} className="h-10 w-full" />)}</div>
        ) : rows.length === 0 ? (
          <p className="px-6 py-12 text-center text-base text-text-3">Nothing matches that filter in the latest requests.</p>
        ) : (
          <ul className="divide-y divide-hair">
            {rows.map((r) => (
              <FeedRow key={r.id} r={r} fresh={fresh.has(r.id)} open={open === r.id} onToggle={() => setOpen(open === r.id ? null : r.id)} />
            ))}
          </ul>
        )}
      </Panel>
    </section>
  );
}

function FeedRow({ r, fresh, open, onToggle }: { r: RecentRequest; fresh: boolean; open: boolean; onToggle: () => void }) {
  const lane = laneOf(r.model_type, r.model_tier);
  const backend = backendOf(r.backend);

  return (
    <li className={cn(fresh && "arrive")}>
      <button onClick={onToggle} aria-expanded={open} className="grid w-full grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-4 px-5 py-3.5 text-left transition-colors hover:bg-raised/60 sm:grid-cols-[auto_minmax(0,1.2fr)_minmax(0,1fr)_6rem_6rem_7rem_auto] sm:px-6">
        {r.success ? (
          <span className="h-2.5 w-2.5 rounded-full bg-ok" aria-label="Succeeded" />
        ) : (
          <CircleAlert className="h-4 w-4 text-bad" aria-label="Failed" />
        )}
        <span className="min-w-0">
          <span className="block truncate text-base font-semibold text-text">{r.project_name}</span>
          <span className="flex items-center gap-2 truncate text-sm text-text-3">
            <LaneDot scope={lane.scope} className="h-2 w-2" />
            {lane.label}
            <span className="sm:hidden">, {modelShortName(r.model_id)}</span>
          </span>
        </span>
        <span className="hidden min-w-0 sm:block">
          <span className="block truncate text-base text-text">{modelShortName(r.model_id)}</span>
          <span className="flex items-center gap-1.5 truncate text-sm text-text-3">
            <backend.icon className="h-3.5 w-3.5 shrink-0" aria-hidden />
            {backend.label}
            {r.streamed && ", streamed"}
          </span>
        </span>
        <span className="tnum hidden text-right text-base text-text-2 sm:block">{compact(r.total_tokens)}</span>
        <span className="tnum hidden text-right text-base text-text-2 sm:block">{ms(r.response_time_ms)}</span>
        <span className="tnum hidden text-right text-base sm:block">
          {r.credit_cost_usd != null ? (
            <span className="text-text-2">{money(Number(r.credit_cost_usd))} <span className="text-text-3">cr</span></span>
          ) : (
            <span className={Number(r.cost_usd) > 0 ? "font-semibold text-text" : "text-text-3"}>{money(Number(r.cost_usd))}</span>
          )}
        </span>
        <span className="flex items-center gap-2 text-sm text-text-3">
          {ago(r.created_at)}
          <ChevronDown className={cn("h-4 w-4 transition-transform duration-300", open && "rotate-180")} aria-hidden />
        </span>
      </button>
      {open && (
        <div className="grid gap-5 border-t border-hair bg-sunken/70 px-6 py-5 animate-lift sm:grid-cols-4">
          <Detail label="Answered by" value={`${backend.label}${backend.billed ? ", billed" : ""}`} />
          <Detail label="Model" value={r.model_id} />
          <Detail label="Tokens in and out" value={`${count(r.input_tokens ?? 0)} in, ${count(r.output_tokens ?? 0)} out`} />
          <Detail
            label="Cost"
            value={
              r.credit_cost_usd != null
                ? `${money(Number(r.credit_cost_usd))} of credit`
                : Number(r.cost_usd) > 0
                  ? `${money(Number(r.cost_usd))} billed`
                  : "Free"
            }
          />
          {r.avoided_cost_usd != null && Number(r.avoided_cost_usd) > 0 && (
            <Detail label="A paid model would have charged" value={money(Number(r.avoided_cost_usd))} />
          )}
          <Detail label="Time" value={`${ms(r.response_time_ms)}, ${new Date(r.created_at).toLocaleString()}`} />
          {r.error_message && (
            <div className={cn("rounded-xl px-4 py-3 sm:col-span-4", r.success ? "bg-panel ring-1 ring-hair" : "bg-bad/[0.08] ring-1 ring-bad/30")}>
              <p className="text-sm text-text-2">{r.error_message}</p>
            </div>
          )}
        </div>
      )}
    </li>
  );
}

export function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <p className="text-sm text-text-3">{label}</p>
      <p className="mt-1 break-words text-base font-semibold text-text">{value}</p>
    </div>
  );
}

