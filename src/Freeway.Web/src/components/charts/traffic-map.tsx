"use client";

import { useMemo, useState } from "react";
import { BarChart3, Table2 } from "lucide-react";
import { Segmented } from "@/components/ui/segmented";
import { useReducedMotion, useWidth } from "@/lib/hooks/motion";
import { LANES, LANE_ORDER, type LaneKey } from "@/lib/theme/lanes";
import { BACKEND_ORDER, backendOf } from "@/lib/theme/backends";
import { cn } from "@/lib/utils/cn";
import { compact, count, pct } from "@/lib/utils/format";
import type { BackendKey, Flow } from "@/lib/types";

type Metric = "requests" | "tokens";
type Col = "app" | "lane" | "backend";

interface Node {
  col: Col;
  key: string;
  label: string;
  value: number;
  y: number;
  h: number;
  scope?: string;
  billed?: boolean;
}

interface Ribbon {
  id: string;
  side: "in" | "out";
  flow: Flow & { app: string; value: number };
  x0: number;
  x1: number;
  y0: number;
  y1: number;
  h: number;
  scope: string;
}

type Focus =
  | { kind: "node"; col: Col; key: string }
  | { kind: "flow"; id: string }
  | null;

const NODE_W = 12;
const GAP = 16;
const MAX_APPS = 7;
const PAD_Y = 8;

/**
 * The whole gateway in one picture: traffic leaves each app, takes the lane it asked
 * for, and arrives at whatever answered it. Ribbon width is volume. Colour is the
 * lane, carried across both halves so a request can be followed by eye; apps and
 * answerers stay ink, named and counted beside their bars.
 *
 * Every ribbon is one (app, lane, answerer) flow, drawn on both sides of the lane, so
 * pointing at anything lights exactly the traffic that went through it.
 */
export function TrafficMap({
  flows,
  height = 460,
  days,
  compactLabels = false,
}: {
  flows: Flow[];
  height?: number;
  days: number;
  /** Hide the apps column (a single project's own page has only one app). */
  compactLabels?: boolean;
}) {
  const [metric, setMetric] = useState<Metric>("requests");
  const [view, setView] = useState<"map" | "table">("map");
  const [focus, setFocus] = useState<Focus>(null);
  const [tip, setTip] = useState<{ x: number; y: number; content: React.ReactNode } | null>(null);
  const [ref, measured] = useWidth<HTMLDivElement>();
  const reduced = useReducedMotion();
  const width = Math.max(measured, 760);
  const showApps = !compactLabels;

  const layout = useMemo(() => {
    const valueOf = (f: Flow) => (metric === "requests" ? f.requests : f.tokens);

    // Fold the long tail of apps into one node so the column stays readable.
    const appTotals = new Map<string, { name: string; value: number }>();
    for (const f of flows) {
      const e = appTotals.get(f.project_id) ?? { name: f.project_name, value: 0 };
      e.value += valueOf(f);
      appTotals.set(f.project_id, e);
    }
    const ranked = [...appTotals.entries()].sort((a, b) => b[1].value - a[1].value);
    const keep = new Set(ranked.slice(0, MAX_APPS - (ranked.length > MAX_APPS ? 1 : 0)).map(([id]) => id));
    const appOf = (f: Flow) => (keep.has(f.project_id) ? f.project_id : "__other");

    const all = flows
      .map((f) => ({ ...f, app: appOf(f), value: valueOf(f) }))
      .filter((f) => f.value > 0);
    const total = all.reduce((s, f) => s + f.value, 0);

    const appOrder = [...ranked.filter(([id]) => keep.has(id)).map(([id]) => id), ...(ranked.length > keep.size ? ["__other"] : [])];
    const laneOrder = ([...LANE_ORDER, "other"] as LaneKey[]).filter((k) => all.some((f) => (f.lane in LANES ? f.lane : "other") === k));
    const backendOrder = BACKEND_ORDER.filter((k) => all.some((f) => f.backend === k));

    const laneKey = (f: Flow) => (f.lane in LANES ? f.lane : "other") as LaneKey;

    const sum = (pred: (f: (typeof all)[number]) => boolean) => all.filter(pred).reduce((s, f) => s + f.value, 0);

    const cols: Record<Col, { key: string; label: string; value: number; scope?: string; billed?: boolean }[]> = {
      app: showApps
        ? appOrder.map((id) => ({
            key: id,
            label: id === "__other" ? "Other apps" : appTotals.get(id)!.name,
            value: sum((f) => f.app === id),
          }))
        : [],
      lane: laneOrder.map((k) => ({ key: k, label: LANES[k].label, value: sum((f) => laneKey(f) === k), scope: LANES[k].scope })),
      backend: backendOrder.map((k) => ({
        key: k,
        label: backendOf(k).label,
        value: sum((f) => f.backend === k),
        billed: backendOf(k).billed,
      })),
    };

    const usable = height - PAD_Y * 2;
    const activeCols = (Object.keys(cols) as Col[]).filter((c) => cols[c].length > 0);
    const k = total
      ? Math.min(...activeCols.map((c) => (usable - (cols[c].length - 1) * GAP) / total))
      : 0;

    const labelL = showApps ? 176 : 24;
    const labelR = 210;
    const xs: Record<Col, number> = showApps
      ? { app: labelL, lane: (labelL + width - labelR - NODE_W) / 2, backend: width - labelR - NODE_W }
      : { app: 0, lane: labelL + 120, backend: width - labelR - NODE_W };

    const nodes: Node[] = [];
    for (const c of activeCols) {
      const heights = cols[c].map((n) => Math.max(3, n.value * k));
      const used = heights.reduce((a, b) => a + b, 0) + (cols[c].length - 1) * GAP;
      let y = PAD_Y + (usable - used) / 2;
      cols[c].forEach((n, i) => {
        nodes.push({ col: c, ...n, y, h: heights[i] });
        y += heights[i] + GAP;
      });
    }
    const node = (c: Col, key: string) => nodes.find((n) => n.col === c && n.key === key)!;

    // Offsets within each node, so ribbons stack in a stable order.
    const cursor = new Map<string, number>();
    const take = (c: Col, key: string, h: number) => {
      const n = node(c, key);
      const id = `${c}:${key}`;
      const at = cursor.get(id) ?? n.y;
      cursor.set(id, at + h);
      return at;
    };

    const ribbons: Ribbon[] = [];
    const idx = (arr: string[], v: string) => arr.indexOf(v);
    const flowId = (f: (typeof all)[number]) => `${f.app}|${laneKey(f)}|${f.backend}|${f.project_id}`;

    if (showApps) {
      // App → lane, ordered by lane (then answerer) leaving each app...
      const leaving = [...all].sort(
        (a, b) =>
          idx(appOrder, a.app) - idx(appOrder, b.app) ||
          idx(laneOrder, laneKey(a)) - idx(laneOrder, laneKey(b)) ||
          idx(backendOrder, a.backend) - idx(backendOrder, b.backend)
      );
      const y0s = new Map<string, number>();
      for (const f of leaving) y0s.set(flowId(f), take("app", f.app, f.value * k));
      // ...and by app arriving at each lane.
      const arriving = [...all].sort(
        (a, b) =>
          idx(laneOrder, laneKey(a)) - idx(laneOrder, laneKey(b)) ||
          idx(appOrder, a.app) - idx(appOrder, b.app) ||
          idx(backendOrder, a.backend) - idx(backendOrder, b.backend)
      );
      for (const f of arriving) {
        const id = flowId(f);
        ribbons.push({
          id,
          side: "in",
          flow: f,
          x0: xs.app + NODE_W,
          x1: xs.lane,
          y0: y0s.get(id)!,
          y1: take("lane", laneKey(f), f.value * k),
          h: f.value * k,
          scope: LANES[laneKey(f)].scope,
        });
      }
    }

    // Lane → answerer, ordered by answerer leaving each lane, by lane arriving.
    cursor.forEach((_, key) => key.startsWith("lane:") && cursor.delete(key));
    const out = [...all].sort(
      (a, b) =>
        idx(laneOrder, laneKey(a)) - idx(laneOrder, laneKey(b)) ||
        idx(backendOrder, a.backend) - idx(backendOrder, b.backend) ||
        idx(appOrder, a.app) - idx(appOrder, b.app)
    );
    const y0out = new Map<string, number>();
    for (const f of out) y0out.set(flowId(f), take("lane", laneKey(f), f.value * k));
    const into = [...all].sort(
      (a, b) =>
        idx(backendOrder, a.backend) - idx(backendOrder, b.backend) ||
        idx(laneOrder, laneKey(a)) - idx(laneOrder, laneKey(b)) ||
        idx(appOrder, a.app) - idx(appOrder, b.app)
    );
    for (const f of into) {
      const id = flowId(f);
      ribbons.push({
        id,
        side: "out",
        flow: f,
        x0: xs.lane + NODE_W,
        x1: xs.backend,
        y0: y0out.get(id)!,
        y1: take("backend", f.backend, f.value * k),
        h: f.value * k,
        scope: LANES[laneKey(f)].scope,
      });
    }

    return { nodes, ribbons, total, xs, laneKey, appTotals };
  }, [flows, metric, height, width, showApps]);

  const lit = (r: Ribbon) => {
    if (!focus) return true;
    if (focus.kind === "flow") return r.id === focus.id;
    const f = r.flow;
    if (focus.col === "app") return f.app === focus.key;
    if (focus.col === "lane") return layout.laneKey(f) === focus.key;
    return f.backend === focus.key;
  };
  const nodeLit = (n: Node) => {
    if (!focus) return true;
    if (focus.kind === "node" && focus.col === n.col && focus.key === n.key) return true;
    return layout.ribbons.some(
      (r) =>
        lit(r) &&
        ((n.col === "app" && r.flow.app === n.key) ||
          (n.col === "lane" && layout.laneKey(r.flow) === n.key) ||
          (n.col === "backend" && r.flow.backend === n.key))
    );
  };

  const fmt = (v: number) => (metric === "requests" ? compact(v) : `${compact(v)} tok`);
  const share = (v: number) => (layout.total ? pct((v / layout.total) * 100, v / layout.total < 0.1 ? 1 : 0) : "0%");
  const unit = metric === "requests" ? "requests" : "tokens";

  const ribbonPath = (r: Ribbon) => {
    const xm = (r.x0 + r.x1) / 2;
    const h = Math.max(r.h, 0.75);
    return `M${r.x0},${r.y0}C${xm},${r.y0} ${xm},${r.y1} ${r.x1},${r.y1}L${r.x1},${r.y1 + h}C${xm},${r.y1 + h} ${xm},${r.y0 + h} ${r.x0},${r.y0 + h}Z`;
  };
  const centre = (r: Ribbon) => {
    const xm = (r.x0 + r.x1) / 2;
    const a = r.y0 + r.h / 2;
    const b = r.y1 + r.h / 2;
    return `M${r.x0},${a}C${xm},${a} ${xm},${b} ${r.x1},${b}`;
  };

  const showTip = (e: React.MouseEvent, content: React.ReactNode) => {
    const box = (e.currentTarget as SVGElement).ownerSVGElement!.getBoundingClientRect();
    setTip({ x: e.clientX - box.left, y: e.clientY - box.top, content });
  };

  const flowTip = (r: Ribbon) => {
    const f = r.flow;
    const appName = f.app === "__other" ? "Other apps" : f.project_name;
    return (
      <>
        <p className="tnum text-xl font-bold text-text">{fmt(f.value)}</p>
        <p className="mt-0.5 text-sm text-text-3">{share(f.value)} of all {unit}</p>
        <p className="mt-2 text-sm text-text">
          {appName} <span className="text-text-3">asked for</span> {LANES[layout.laneKey(f)].label}
        </p>
        <p className="text-sm text-text">
          <span className="text-text-3">answered by</span> {backendOf(f.backend).label}
          {backendOf(f.backend).billed && <span className="text-text-3">, billed</span>}
        </p>
      </>
    );
  };

  const nodeTip = (n: Node) => (
    <>
      <p className="text-sm font-semibold text-text-2">{n.label}</p>
      <p className="tnum mt-0.5 text-xl font-bold text-text">{fmt(n.value)}</p>
      <p className="mt-0.5 text-sm text-text-3">{share(n.value)} of all {unit}</p>
      {n.col === "backend" && <p className="mt-2 max-w-[15rem] text-sm text-text-2">{backendOf(n.key).blurb}</p>}
      {n.col === "lane" && LANES[n.key as LaneKey]?.model && (
        <p className="mt-2 font-mono text-xs text-text-2">&quot;model&quot;: &quot;{LANES[n.key as LaneKey].model}&quot;</p>
      )}
    </>
  );

  const nodes = layout.nodes;

  return (
    <div>
      <div className="flex flex-wrap items-start justify-between gap-4 px-5 pt-5 sm:px-6">
        <div className="max-w-2xl">
          <h2 className="text-xl font-bold text-text">Traffic map</h2>
          <p className="mt-1 text-base text-text-3">
            {showApps
              ? `Every request in the last ${days} days: the app that sent it, the lane it asked for, and what answered it. Point at anything to follow its traffic.`
              : `This project's requests in the last ${days} days, from the lane each asked for to what answered it.`}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Segmented
            size="sm"
            label="Measure"
            value={metric}
            onChange={setMetric}
            segments={[
              { value: "requests", label: "Requests" },
              { value: "tokens", label: "Tokens" },
            ]}
          />
          <button
            onClick={() => setView(view === "map" ? "table" : "map")}
            className="grid h-9 w-9 place-items-center rounded-lg text-text-3 ring-1 ring-hair transition-colors hover:bg-raised hover:text-text"
            aria-label={view === "map" ? "Show as a table" : "Show as a map"}
            title={view === "map" ? "Show as a table" : "Show as a map"}
          >
            {view === "map" ? <Table2 className="h-4 w-4" /> : <BarChart3 className="h-4 w-4" />}
          </button>
        </div>
      </div>

      {layout.total === 0 ? (
        <p className="px-6 py-20 text-center text-base text-text-3">No traffic in this window yet.</p>
      ) : view === "table" ? (
        <div className="mt-4 max-h-[28rem] overflow-auto px-5 pb-5 sm:px-6">
          <table className="w-full text-sm">
            <thead className="sticky top-0 bg-panel text-left text-text-3">
              <tr>
                {showApps && <th className="py-2 pr-4 font-semibold">App</th>}
                <th className="py-2 pr-4 font-semibold">Lane</th>
                <th className="py-2 pr-4 font-semibold">Answered by</th>
                <th className="py-2 pr-4 text-right font-semibold">Requests</th>
                <th className="py-2 pr-4 text-right font-semibold">Tokens</th>
                <th className="py-2 text-right font-semibold">Share</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-hair">
              {[...flows].sort((a, b) => b.requests - a.requests).map((f) => (
                <tr key={`${f.project_id}-${f.lane}-${f.backend}`} className="text-text-2">
                  {showApps && <td className="py-2 pr-4 text-text">{f.project_name}</td>}
                  <td className="py-2 pr-4">
                    <span className="inline-flex items-center gap-2">
                      <span className={cn(LANES[(f.lane in LANES ? f.lane : "other") as LaneKey].scope, "h-2 w-2 rounded-full lane-bg")} />
                      {LANES[(f.lane in LANES ? f.lane : "other") as LaneKey].label}
                    </span>
                  </td>
                  <td className="py-2 pr-4">{backendOf(f.backend).label}</td>
                  <td className="tnum py-2 pr-4 text-right text-text">{count(f.requests)}</td>
                  <td className="tnum py-2 pr-4 text-right">{compact(f.tokens)}</td>
                  <td className="tnum py-2 text-right">{share(metric === "requests" ? f.requests : f.tokens)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div ref={ref} className="relative mt-4 overflow-x-auto pb-4" onMouseLeave={() => { setFocus(null); setTip(null); }}>
          {measured > 0 && (
            <svg width={width} height={height} className="block" role="img" aria-label="Traffic from apps through lanes to what answered">
              <defs>
                <style>{`@keyframes wipe{from{clip-path:inset(0 100% 0 0)}to{clip-path:inset(0 0 0 0)}}`}</style>
              </defs>

              {/* Column headings, quiet, above each column. */}
              {showApps && (
                <text x={layout.xs.app + NODE_W} y={0} dy="-0.2em" textAnchor="end" className="hidden" />
              )}

              <g key={`${metric}-${flows.length}`} style={{ animation: "wipe 1400ms cubic-bezier(0.65,0,0.35,1) both" }}>
                {layout.ribbons.map((r) => {
                  const on = lit(r);
                  return (
                    <path
                      key={`${r.side}-${r.id}`}
                      d={ribbonPath(r)}
                      className={cn(r.scope, "cursor-pointer transition-[fill-opacity] duration-300")}
                      fill="rgb(var(--lane))"
                      fillOpacity={focus ? (on ? 0.72 : 0.06) : 0.36}
                      onMouseEnter={(e) => { setFocus({ kind: "flow", id: r.id }); showTip(e, flowTip(r)); }}
                      onMouseMove={(e) => showTip(e, flowTip(r))}
                    />
                  );
                })}

                {/* Traffic: dashes running the length of each ribbon, toward what answered. */}
                {!reduced &&
                  layout.ribbons
                    .filter((r) => r.h >= 4)
                    .map((r, i) => (
                      <path
                        key={`flow-${r.side}-${r.id}`}
                        d={centre(r)}
                        fill="none"
                        stroke="rgb(var(--text))"
                        strokeOpacity={focus ? (lit(r) ? 0.55 : 0) : 0.22}
                        strokeWidth={Math.min(2, r.h / 4)}
                        strokeLinecap="round"
                        strokeDasharray="1.5 30"
                        className="pointer-events-none transition-[stroke-opacity] duration-300"
                        style={{ animation: `flow ${2.6 + (i % 5) * 0.35}s linear infinite` }}
                      />
                    ))}
              </g>

              {nodes.map((n) => {
                const on = nodeLit(n);
                const x = layout.xs[n.col];
                const fill = n.col === "lane" ? "rgb(var(--lane))" : n.billed ? "rgb(var(--text))" : "rgb(var(--text-2))";
                return (
                  <g
                    key={`${n.col}-${n.key}`}
                    className={cn(n.scope, "cursor-pointer transition-opacity duration-300")}
                    opacity={on ? 1 : 0.3}
                    onMouseEnter={(e) => { setFocus({ kind: "node", col: n.col, key: n.key }); showTip(e, nodeTip(n)); }}
                    onMouseMove={(e) => showTip(e, nodeTip(n))}
                  >
                    <rect x={x - 6} y={n.y - 4} width={NODE_W + 12} height={n.h + 8} fill="transparent" />
                    <rect x={x} y={n.y} width={NODE_W} height={n.h} rx={3} fill={fill} />
                    <NodeLabel node={n} x={x} fmt={fmt} share={share} />
                  </g>
                );
              })}
            </svg>
          )}

          {tip && (
            <div
              className="pointer-events-none absolute z-20 min-w-[13rem] max-w-[18rem] rounded-xl border border-hair-bright bg-overlay px-4 py-3 shadow-overlay"
              style={{
                left: Math.min(tip.x + 16, width - 300),
                top: Math.max(8, tip.y - 20),
              }}
            >
              {tip.content}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function NodeLabel({
  node: n,
  x,
  fmt,
  share,
}: {
  node: Node;
  x: number;
  fmt: (v: number) => string;
  share: (v: number) => string;
}) {
  const mid = n.y + n.h / 2;
  const tight = n.h < 34;
  const halo = { paintOrder: "stroke", stroke: "rgb(var(--panel))", strokeWidth: 5, strokeLinejoin: "round" as const };

  if (n.col === "app") {
    return (
      <text x={x - 14} y={mid} textAnchor="end" dy={tight ? "0.35em" : "-0.15em"}>
        <tspan className="fill-text text-sm font-semibold">{n.label}</tspan>
        {tight ? (
          <tspan className="tnum fill-text-3 text-xs" dx="8">{fmt(n.value)}</tspan>
        ) : (
          <tspan x={x - 14} dy="1.35em" className="tnum fill-text-3 text-xs">{fmt(n.value)} · {share(n.value)}</tspan>
        )}
      </text>
    );
  }

  if (n.col === "lane") {
    return (
      <text x={x + NODE_W + 10} y={mid} dy={tight ? "0.35em" : "-0.15em"} style={halo}>
        <tspan className="fill-text text-sm font-bold">{n.label}</tspan>
        {tight ? (
          <tspan className="tnum fill-text-2 text-xs" dx="8">{fmt(n.value)}</tspan>
        ) : (
          <tspan x={x + NODE_W + 10} dy="1.35em" className="tnum fill-text-2 text-xs">{fmt(n.value)}</tspan>
        )}
      </text>
    );
  }

  return (
    <text x={x + NODE_W + 14} y={mid} dy={tight ? "0.35em" : "-0.15em"}>
      <tspan className="fill-text text-sm font-semibold">{n.label}</tspan>
      {tight ? (
        <tspan className="tnum fill-text-3 text-xs" dx="8">
          {fmt(n.value)}
          {n.billed ? ", billed" : ""}
        </tspan>
      ) : (
        <tspan x={x + NODE_W + 14} dy="1.35em" className="tnum fill-text-3 text-xs">
          {fmt(n.value)} · {share(n.value)}
          {n.billed ? ", billed" : ""}
        </tspan>
      )}
    </text>
  );
}

export type { BackendKey };
