"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Search, SlidersHorizontal, Star } from "lucide-react";
import { PageHead } from "@/components/shell/page-head";
import { Panel, PanelHead } from "@/components/ui/panel";
import { Field } from "@/components/ui/field";
import { Button } from "@/components/ui/button";
import { Tag } from "@/components/ui/tag";
import { Segmented } from "@/components/ui/segmented";
import { PulseRows } from "@/components/ui/pulse";
import { Empty, Failed } from "@/components/ui/state";
import { useToast } from "@/components/ui/toast";
import { modelsApi } from "@/lib/api/models";
import { LANES, TIER_LANE, type LaneKey } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";
import { compact, perMillion, splitModelId } from "@/lib/utils/format";
import { PAID_TIERS, type ModelInfo, type PaidTier } from "@/lib/types";

type Tab = "free" | "paid" | "image";

export default function ModelsPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const [tab, setTab] = useState<Tab>("free");
  const [tier, setTier] = useState<PaidTier>("low");
  const [search, setSearch] = useState("");

  const laneKey: LaneKey = tab === "paid" ? TIER_LANE[tier] : tab;
  const lane = LANES[laneKey];

  const free = useQuery({ queryKey: ["models", "free"], queryFn: () => modelsApi.getFreeModels() });
  const paid = useQuery({
    queryKey: ["models", "paid", tier],
    queryFn: () => modelsApi.getPaidModelsForTier(tier),
  });
  const image = useQuery({ queryKey: ["models", "image"], queryFn: () => modelsApi.getImageModels() });

  const selFree = useQuery({ queryKey: ["model", "free"], queryFn: () => modelsApi.getSelectedFreeModel() });
  const selPaid = useQuery({
    queryKey: ["model", "paid", tier],
    queryFn: () => modelsApi.getSelectedPaidModelForTier(tier),
  });
  const selImage = useQuery({ queryKey: ["model", "image"], queryFn: () => modelsApi.getSelectedImageModel() });

  const pick = useMutation({
    mutationFn: (modelId: string) =>
      tab === "free"
        ? modelsApi.setSelectedFreeModel(modelId)
        : tab === "paid"
          ? modelsApi.setSelectedPaidModelForTier(tier, modelId)
          : modelsApi.setSelectedImageModel(modelId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["model"] });
      qc.invalidateQueries({ queryKey: ["overview"] });
      toast(`${lane.label} lane now routes to your pick`, "success");
    },
    onError: () => toast(`Could not change the ${lane.label.toLowerCase()} lane`, "error"),
  });

  const active = tab === "free" ? free : tab === "paid" ? paid : image;
  const selectedId =
    tab === "free"
      ? selFree.data?.model_id
      : tab === "paid"
        ? selPaid.data?.model_id
        : selImage.data?.model_id;

  const models = useMemo(() => {
    const list = active.data ?? [];
    if (!search) return list;
    const q = search.toLowerCase();
    return list.filter(
      (m) => m.model_id.toLowerCase().includes(q) || m.model_name.toLowerCase().includes(q)
    );
  }, [active.data, search]);

  return (
    <div className="space-y-6">
      <PageHead
        title="Routing"
        lede="Each lane points at one model. Requests name the lane, not the model, so this is where routing actually changes."
        actions={
          <Segmented
            label="Model kind"
            value={tab}
            onChange={setTab}
            segments={[
              { value: "free", label: "Free", scope: LANES.free.scope },
              { value: "paid", label: "Paid", scope: lane.scope },
              { value: "image", label: "Image", scope: LANES.image.scope },
            ]}
          />
        }
      />

      {/* The lane currently being edited, stated plainly with the value a caller sends. */}
      <div className={cn(lane.scope, "flex flex-wrap items-center gap-x-5 gap-y-3 rounded-panel border lane-edge lane-soft px-4 py-3")}>
        <span className="flex items-center gap-2">
          <lane.icon className="h-4 w-4 lane-fg" aria-hidden />
          <span className="text-sm font-semibold text-text">{lane.label} lane</span>
        </span>
        <code className="rounded bg-base/40 px-2 py-1 font-mono text-xs lane-fg">
          {'"model": "'}
          {lane.model}
          {'"'}
        </code>
        <p className="min-w-0 flex-1 text-xs text-text-2">{lane.blurb}</p>

        {tab === "paid" && (
          <Segmented
            label="Paid tier"
            value={tier}
            onChange={setTier}
            segments={PAID_TIERS.map((t) => ({
              value: t,
              label: LANES[TIER_LANE[t]].label,
              scope: LANES[TIER_LANE[t]].scope,
            }))}
          />
        )}
      </div>

      <Panel>
        <PanelHead
          title={`${compact(active.data?.length ?? 0)} models`}
          meta={
            tab === "paid"
              ? "In fallback order: curated first, then cheapest in the band"
              : tab === "free"
                ? "Ranked by context length"
                : "Ranked by what they cost to generate"
          }
          action={
            <Field
              placeholder="Filter"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              lead={<Search className="h-3.5 w-3.5" />}
              aria-label="Filter models"
              className="h-8 w-44 sm:w-60"
            />
          }
        />

        {active.isLoading ? (
          <PulseRows rows={8} />
        ) : active.isError ? (
          <div className="p-4">
            <Failed
              title="Could not load models"
              body="The catalog refreshes daily. If this persists the gateway may not have reached its providers."
              onRetry={() => active.refetch()}
            />
          </div>
        ) : models.length === 0 ? (
          <Empty
            className={lane.scope}
            icon={search ? Search : SlidersHorizontal}
            title={search ? "Nothing matches that" : "No models in this lane"}
            body={
              search
                ? "Try part of a vendor or model name, like 'gemini' or 'mini'."
                : "The gateway has not cached anything here yet. Check back after the next refresh."
            }
            action={
              search ? (
                <Button onClick={() => setSearch("")}>Clear filter</Button>
              ) : undefined
            }
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[46rem]">
              <thead>
                <tr className="border-b border-hair text-left">
                  <th className="px-4 py-2 text-2xs font-medium uppercase text-text-3">Model</th>
                  <th className="px-3 py-2 text-right text-2xs font-medium uppercase text-text-3">Context</th>
                  <th className="px-3 py-2 text-right text-2xs font-medium uppercase text-text-3">In /M</th>
                  <th className="px-3 py-2 text-right text-2xs font-medium uppercase text-text-3">Out /M</th>
                  <th className="w-28 px-4 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-hair">
                {models.map((m) => (
                  <ModelRow
                    key={m.model_id}
                    model={m}
                    laneScope={lane.scope}
                    selected={m.model_id === selectedId}
                    busy={pick.isPending}
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
  laneScope,
  selected,
  busy,
  onPick,
}: {
  model: ModelInfo;
  laneScope: string;
  selected: boolean;
  busy: boolean;
  onPick: () => void;
}) {
  const { vendor, name } = splitModelId(model.model_id);

  return (
    <tr className={cn(laneScope, "group transition-colors", selected ? "lane-soft" : "hover:bg-raised")}>
      <td className="px-4 py-2.5">
        <div className="flex items-center gap-2">
          {selected && <span className="h-1.5 w-1.5 shrink-0 rounded-full lane-bg" aria-hidden />}
          <span className="truncate font-mono text-xs text-text">
            {vendor && <span className="text-text-3">{vendor}/</span>}
            {name}
          </span>
          {model.is_curated && (
            <Tag tone="lane" className="shrink-0">
              <Star className="h-2.5 w-2.5" aria-hidden />
              curated
            </Tag>
          )}
          {model.rank != null && model.rank <= 3 && !model.is_curated && (
            <Tag className="shrink-0">#{model.rank}</Tag>
          )}
        </div>
        {model.model_name && model.model_name !== name && (
          <p className="mt-0.5 truncate text-xs text-text-3">{model.model_name}</p>
        )}
      </td>
      <td className="fig px-3 py-2.5 text-right text-xs text-text-2">
        {compact(model.context_length)}
      </td>
      <td className="fig px-3 py-2.5 text-right text-xs text-text-2">
        {perMillion(model.pricing?.prompt)}
      </td>
      <td className="fig px-3 py-2.5 text-right text-xs text-text-2">
        {perMillion(model.pricing?.completion)}
      </td>
      <td className="px-4 py-2.5 text-right">
        {selected ? (
          <span className="inline-flex items-center gap-1.5 text-xs font-medium lane-fg">
            <Check className="h-3.5 w-3.5" aria-hidden />
            Routing
          </span>
        ) : (
          <Button
            size="sm"
            busy={busy}
            onClick={onPick}
            className="opacity-0 transition-opacity focus-visible:opacity-100 group-hover:opacity-100"
          >
            Route here
          </Button>
        )}
      </td>
    </tr>
  );
}
