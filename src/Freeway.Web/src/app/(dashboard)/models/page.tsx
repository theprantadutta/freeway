"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowRight, Check, Search, SlidersHorizontal, Star } from "lucide-react";
import { PageHead } from "@/components/shell/page-head";
import { Panel, PanelHead } from "@/components/ui/panel";
import { Field } from "@/components/ui/field";
import { Button } from "@/components/ui/button";
import { Tag } from "@/components/ui/tag";
import { Pulse } from "@/components/ui/pulse";
import { Empty, Failed } from "@/components/ui/state";
import { useToast } from "@/components/ui/toast";
import { modelsApi } from "@/lib/api/models";
import { creditsApi } from "@/lib/api/credits";
import { analyticsApi } from "@/lib/api/analytics";
import { LANES, LANE_ORDER, type LaneKey } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";
import { compact, perMillion, splitModelId } from "@/lib/utils/format";
import type { ModelInfo, PaidTier } from "@/lib/types";

const PAID: Partial<Record<LaneKey, PaidTier>> = { low: "low", moderate: "moderate", premium: "premium" };

/**
 * Where each lane's requests go. A lane is what callers name; this page shows, for
 * every lane, the order its requests are actually tried in, and lets you pick the
 * model at the end of that order from the catalogue below.
 */
export default function RoutingPage() {
  const qc = useQueryClient();
  const { toast } = useToast();
  const [laneKey, setLaneKey] = useState<LaneKey>("premium");
  const [search, setSearch] = useState("");

  const lane = LANES[laneKey];
  const tier = PAID[laneKey];

  const credit = useQuery({ queryKey: ["credits"], queryFn: creditsApi.getOverview });
  const bridge = useQuery({ queryKey: ["local-claude"], queryFn: analyticsApi.getLocalClaude });

  const selected = {
    free: useQuery({ queryKey: ["model", "free"], queryFn: () => modelsApi.getSelectedFreeModel() }),
    low: useQuery({ queryKey: ["model", "paid", "low"], queryFn: () => modelsApi.getSelectedPaidModelForTier("low") }),
    moderate: useQuery({ queryKey: ["model", "paid", "moderate"], queryFn: () => modelsApi.getSelectedPaidModelForTier("moderate") }),
    premium: useQuery({ queryKey: ["model", "paid", "premium"], queryFn: () => modelsApi.getSelectedPaidModelForTier("premium") }),
    image: useQuery({ queryKey: ["model", "image"], queryFn: () => modelsApi.getSelectedImageModel() }),
  } as const;

  const catalog = useQuery({
    queryKey: ["models", laneKey],
    queryFn: () =>
      laneKey === "free"
        ? modelsApi.getFreeModels()
        : laneKey === "image"
          ? modelsApi.getImageModels()
          : modelsApi.getPaidModelsForTier(tier!),
  });

  const pick = useMutation({
    mutationFn: (modelId: string) =>
      laneKey === "free"
        ? modelsApi.setSelectedFreeModel(modelId)
        : laneKey === "image"
          ? modelsApi.setSelectedImageModel(modelId)
          : modelsApi.setSelectedPaidModelForTier(tier!, modelId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["model"] });
      qc.invalidateQueries({ queryKey: ["overview"] });
      toast(`${lane.label} lane now falls back to your pick`, "success");
    },
    onError: () => toast(`Could not change the ${lane.label.toLowerCase()} lane`, "error"),
  });

  const selectedId = (selected as Record<string, { data?: { model_id: string } }>)[laneKey]?.data?.model_id;

  const models = useMemo(() => {
    const list = catalog.data ?? [];
    if (!search) return list;
    const q = search.toLowerCase();
    return list.filter((m) => m.model_id.toLowerCase().includes(q) || m.model_name.toLowerCase().includes(q));
  }, [catalog.data, search]);

  const creditModel = (k: LaneKey) => credit.data?.lanes.find((l) => l.tier === k)?.model;

  return (
    <div className="space-y-10">
      <PageHead
        title="Routing"
        lede="Callers ask for a lane, never a model. Each paid lane tries what is already paid for first, and only then the model you choose here."
      />

      <section className="reveal grid gap-4 md:grid-cols-2 2xl:grid-cols-5" style={{ ["--d" as string]: "80ms" }} aria-label="Lanes">
        {LANE_ORDER.map((k) => {
          const l = LANES[k];
          const isPaid = !!PAID[k];
          const on = k === laneKey;
          const sel = (selected as Record<string, { data?: { model_id: string }; isLoading: boolean }>)[k];
          const steps: { label: string; note: string; ready?: boolean }[] = [];
          if (isPaid && credit.data?.configured && creditModel(k)) {
            steps.push({ label: creditModel(k)!, note: "Claude credit", ready: credit.data.can_serve });
          }
          if (isPaid && bridge.data?.configured) {
            steps.push({ label: "Claude Code", note: k === "premium" ? "Subscription" : "Spare capacity", ready: bridge.data.can_serve });
          }
          return (
            <button
              key={k}
              onClick={() => { setLaneKey(k); setSearch(""); }}
              aria-pressed={on}
              className={cn(
                l.scope,
                "card group relative flex flex-col p-5 text-left transition-[border-color,transform] duration-300 ease-out hover:-translate-y-0.5",
                on ? "border-[rgb(var(--lane)/0.7)] ring-1 ring-[rgb(var(--lane)/0.5)]" : "hover:border-hair-bright"
              )}
            >
              <span className="flex items-center gap-3">
                <span className="grid h-10 w-10 place-items-center rounded-xl lane-soft">
                  <l.icon className="h-5 w-5 lane-fg" aria-hidden />
                </span>
                <span>
                  <span className="block text-lg font-bold text-text">{l.label}</span>
                  <code className="font-mono text-xs text-text-3">&quot;model&quot;: &quot;{l.model}&quot;</code>
                </span>
              </span>

              <ol className="mt-5 space-y-2.5">
                {steps.map((s) => (
                  <li key={s.label} className="flex items-start gap-2.5">
                    <span className={cn("mt-1.5 h-2 w-2 shrink-0 rounded-full", s.ready ? "bg-ok" : "bg-text-3/60")} aria-hidden />
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-semibold text-text">{s.label}</span>
                      <span className="block text-xs text-text-3">{s.note}</span>
                    </span>
                  </li>
                ))}
                <li className="flex items-start gap-2.5">
                  <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full lane-bg" aria-hidden />
                  <span className="min-w-0">
                    {sel?.isLoading ? (
                      <Pulse className="h-4 w-32" />
                    ) : (
                      <span className="block truncate text-sm font-semibold text-text">
                        {splitModelId(sel?.data?.model_id).name}
                      </span>
                    )}
                    <span className="block text-xs text-text-3">
                      {isPaid ? "OpenRouter, billed, last resort" : k === "free" ? "Free providers, ranked by health" : "OpenRouter, billed"}
                    </span>
                  </span>
                </li>
              </ol>

              <span className={cn("mt-5 flex items-center gap-1.5 text-sm font-semibold", on ? "text-text" : "text-text-3 group-hover:text-text-2")}>
                {on ? "Choosing below" : "Change model"}
                {!on && <ArrowRight className="h-4 w-4 transition-transform duration-200 group-hover:translate-x-0.5" aria-hidden />}
              </span>
            </button>
          );
        })}
      </section>

      <Panel className={cn(lane.scope, "reveal")} style={{ ["--d" as string]: "160ms" }}>
        <PanelHead
          icon={<lane.icon className="h-4 w-4 lane-fg" aria-hidden />}
          title={`${lane.label} lane catalogue`}
          meta={
            PAID[laneKey]
              ? `${compact(catalog.data?.length ?? 0)} models, in fallback order: curated first, then cheapest in the band`
              : laneKey === "free"
                ? `${compact(catalog.data?.length ?? 0)} models, ranked by context length`
                : `${compact(catalog.data?.length ?? 0)} models, ranked by what they cost to generate`
          }
          action={
            <Field
              placeholder="Filter models"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              lead={<Search className="h-4 w-4" />}
              aria-label="Filter models"
              className="h-10 w-52 sm:w-72"
            />
          }
        />

        {catalog.isLoading ? (
          <div className="space-y-2 p-6">{Array.from({ length: 8 }).map((_, i) => <Pulse key={i} className="h-11 w-full" />)}</div>
        ) : catalog.isError ? (
          <div className="p-6">
            <Failed
              title="Could not load models"
              body="The catalogue refreshes daily. If this persists the gateway may not have reached its providers."
              onRetry={() => catalog.refetch()}
            />
          </div>
        ) : models.length === 0 ? (
          <Empty
            icon={search ? Search : SlidersHorizontal}
            title={search ? "Nothing matches that" : "No models in this lane"}
            body={search ? "Try part of a vendor or model name, like 'gemini' or 'mini'." : "The gateway has not cached anything here yet. Check back after the next refresh."}
            action={search ? <Button onClick={() => setSearch("")}>Clear filter</Button> : undefined}
          />
        ) : (
          <div className="mt-4 overflow-x-auto">
            <table className="w-full min-w-[48rem] text-left">
              <thead>
                <tr className="border-y border-hair text-sm text-text-3">
                  <th className="px-6 py-3 font-semibold">Model</th>
                  <th className="px-3 py-3 text-right font-semibold">Context</th>
                  <th className="px-3 py-3 text-right font-semibold">Input, per million</th>
                  <th className="px-3 py-3 text-right font-semibold">Output, per million</th>
                  <th className="w-40 px-6 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-hair">
                {models.map((m) => (
                  <ModelRow
                    key={m.model_id}
                    model={m}
                    selected={m.model_id === selectedId}
                    busy={pick.isPending && pick.variables === m.model_id}
                    disabled={pick.isPending}
                    onPick={() => pick.mutate(m.model_id)}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </div>
  );
}

function ModelRow({
  model,
  selected,
  busy,
  disabled,
  onPick,
}: {
  model: ModelInfo;
  selected: boolean;
  busy: boolean;
  disabled: boolean;
  onPick: () => void;
}) {
  const { vendor, name } = splitModelId(model.model_id);

  return (
    <tr className={cn("group transition-colors", selected ? "lane-soft" : "hover:bg-raised/60")}>
      <td className="px-6 py-3.5">
        <div className="flex flex-wrap items-center gap-2">
          {selected && <span className="h-2.5 w-2.5 shrink-0 rounded-full lane-bg" aria-hidden />}
          <span className="truncate text-base font-semibold text-text">{name}</span>
          {vendor && <span className="text-sm text-text-3">{vendor}</span>}
          {model.is_curated && (
            <Tag tone="lane" className="shrink-0">
              <Star className="h-3 w-3" aria-hidden />
              Curated
            </Tag>
          )}
          {model.rank != null && model.rank <= 3 && !model.is_curated && <Tag className="shrink-0">Top {model.rank}</Tag>}
        </div>
        {model.model_name && model.model_name !== name && (
          <p className="mt-0.5 truncate text-sm text-text-3">{model.model_name}</p>
        )}
      </td>
      <td className="tnum px-3 py-3.5 text-right text-base text-text-2">{compact(model.context_length)}</td>
      <td className="tnum px-3 py-3.5 text-right text-base text-text-2">{perMillion(model.pricing?.prompt)}</td>
      <td className="tnum px-3 py-3.5 text-right text-base text-text-2">{perMillion(model.pricing?.completion)}</td>
      <td className="px-6 py-3.5 text-right">
        {selected ? (
          <span className="inline-flex items-center gap-1.5 text-sm font-bold text-text">
            <Check className="h-4 w-4" aria-hidden />
            In use
          </span>
        ) : (
          <Button
            size="sm"
            busy={busy}
            disabled={disabled}
            onClick={onPick}
            className="opacity-0 transition-opacity focus-visible:opacity-100 group-hover:opacity-100"
          >
            Use this model
          </Button>
        )}
      </td>
    </tr>
  );
}
