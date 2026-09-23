"use client";

import { useState, useMemo } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Search, Check, Star, SlidersHorizontal } from "lucide-react";
import { Header } from "@/components/layout/header";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { modelsApi } from "@/lib/api/models";
import { formatNumber, formatCurrency } from "@/lib/utils/format";
import { cn } from "@/lib/utils/cn";
import { ACCENTS, PAID_TIER_ACCENT, type AccentKey } from "@/lib/theme/accents";
import { PAID_TIERS, type ModelInfo, type PaidTier } from "@/lib/types";

type TabKey = "free" | "paid" | "image";

export default function ModelsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState("");
  const [activeTab, setActiveTab] = useState<TabKey>("free");
  const [paidTier, setPaidTier] = useState<PaidTier>("low");

  // The accent in play follows the tab, and for paid it follows the tier.
  const accentKey: AccentKey =
    activeTab === "paid" ? PAID_TIER_ACCENT[paidTier] : activeTab;
  const accent = ACCENTS[accentKey];

  const freeQuery = useQuery({
    queryKey: ["models", "free"],
    queryFn: () => modelsApi.getFreeModels(),
  });

  const paidQuery = useQuery({
    queryKey: ["models", "paid", paidTier],
    queryFn: () => modelsApi.getPaidModelsForTier(paidTier),
  });

  const imageQuery = useQuery({
    queryKey: ["models", "image"],
    queryFn: () => modelsApi.getImageModels(),
  });

  const { data: selectedFree } = useQuery({
    queryKey: ["model", "free"],
    queryFn: () => modelsApi.getSelectedFreeModel(),
  });

  const { data: selectedPaid } = useQuery({
    queryKey: ["model", "paid", paidTier],
    queryFn: () => modelsApi.getSelectedPaidModelForTier(paidTier),
  });

  const { data: selectedImage } = useQuery({
    queryKey: ["model", "image"],
    queryFn: () => modelsApi.getSelectedImageModel(),
  });

  const setFreeMutation = useMutation({
    mutationFn: (modelId: string) => modelsApi.setSelectedFreeModel(modelId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["model", "free"] });
      toast("Free model selected", "success");
    },
    onError: () => toast("Could not select that free model", "error"),
  });

  const setPaidMutation = useMutation({
    mutationFn: (modelId: string) =>
      modelsApi.setSelectedPaidModelForTier(paidTier, modelId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["model", "paid"] });
      toast(`${ACCENTS[PAID_TIER_ACCENT[paidTier]].label} model selected`, "success");
    },
    onError: () =>
      toast(
        `Could not select that ${ACCENTS[PAID_TIER_ACCENT[paidTier]].label.toLowerCase()} model`,
        "error"
      ),
  });

  const setImageMutation = useMutation({
    mutationFn: (modelId: string) => modelsApi.setSelectedImageModel(modelId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["model", "image"] });
      toast("Image model selected", "success");
    },
    onError: () => toast("Could not select that image model", "error"),
  });

  const activeQuery =
    activeTab === "free" ? freeQuery : activeTab === "paid" ? paidQuery : imageQuery;

  const models = useMemo(() => {
    const list = activeQuery.data ?? [];
    if (!search) return list;
    const q = search.toLowerCase();
    return list.filter(
      (m) =>
        m.model_id.toLowerCase().includes(q) ||
        m.model_name.toLowerCase().includes(q)
    );
  }, [activeQuery.data, search]);

  const selectedModelId =
    activeTab === "free"
      ? selectedFree?.model_id
      : activeTab === "paid"
        ? selectedPaid?.model_id
        : selectedImage?.model_id;

  const isMutating =
    activeTab === "free"
      ? setFreeMutation.isPending
      : activeTab === "paid"
        ? setPaidMutation.isPending
        : setImageMutation.isPending;

  const handleSelectModel = (modelId: string) => {
    if (activeTab === "free") setFreeMutation.mutate(modelId);
    else if (activeTab === "paid") setPaidMutation.mutate(modelId);
    else setImageMutation.mutate(modelId);
  };

  const totalCount = activeQuery.data?.length ?? 0;

  return (
    <div className="flex h-full flex-col">
      <Header
        title="Models"
        subtitle="Choose what each lane routes to"
        actions={
          <Tabs
            defaultValue="free"
            value={activeTab}
            onChange={(v) => setActiveTab(v as TabKey)}
          >
            <TabsList>
              <TabsTrigger value="free" accent={ACCENTS.free.scope}>
                Free
              </TabsTrigger>
              <TabsTrigger value="paid" accent={accent.scope}>
                Paid
              </TabsTrigger>
              <TabsTrigger value="image" accent={ACCENTS.image.scope}>
                Image
              </TabsTrigger>
            </TabsList>
          </Tabs>
        }
      />

      <div className="flex-1 space-y-4 p-4 md:p-6">
        {activeTab === "paid" && (
          <div className="flex flex-col gap-3 rounded-panel border border-line bg-panel p-3 sm:flex-row sm:items-center sm:gap-4">
            <div
              role="tablist"
              aria-label="Paid tier"
              className="flex items-center gap-0.5 rounded-control bg-inset p-0.5"
            >
              {PAID_TIERS.map((tier) => {
                const tierAccent = ACCENTS[PAID_TIER_ACCENT[tier]];
                const isActive = paidTier === tier;
                return (
                  <button
                    key={tier}
                    role="tab"
                    aria-selected={isActive}
                    onClick={() => setPaidTier(tier)}
                    className={cn(
                      "flex items-center gap-1.5 rounded-[0.3125rem] px-3 py-1.5 text-sm font-medium",
                      "transition-[background-color,color] duration-150 ease-swift",
                      isActive
                        ? cn(tierAccent.scope, "bg-panel accent-text shadow-pop")
                        : "text-muted hover:text-ink"
                    )}
                  >
                    <span
                      className={cn(
                        tierAccent.scope,
                        "h-1.5 w-1.5 rounded-full accent-bg"
                      )}
                      aria-hidden
                    />
                    {tierAccent.label}
                  </button>
                );
              })}
            </div>
            <p className={cn(accent.scope, "flex-1 text-xs leading-relaxed text-muted")}>
              {accent.hint} Request it with{" "}
              <code className="rounded bg-inset px-1 py-0.5 font-mono text-ink">
                {accent.model}
              </code>
            </p>
          </div>
        )}

        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <Input
            placeholder="Search by name or ID"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            leading={<Search className="h-4 w-4" />}
            className="sm:max-w-sm"
            aria-label="Search models"
          />
          <p className="text-sm text-subtle">
            {search
              ? `${models.length} of ${totalCount} models`
              : `${totalCount} models`}
            {activeTab === "paid" && " in this tier, in fallback order"}
          </p>
        </div>

        {activeQuery.isLoading ? (
          <SkeletonList count={5} />
        ) : activeQuery.isError ? (
          <ErrorState
            title="Could not load models"
            description="The gateway did not return its model catalog. It refreshes daily, so a restart may be needed."
            onRetry={() => activeQuery.refetch()}
          />
        ) : models.length === 0 ? (
          <EmptyState
            className={accent.scope}
            icon={search ? Search : SlidersHorizontal}
            title={search ? "No models match that" : "No models in this lane"}
            description={
              search
                ? "Try part of the provider or model name, like 'gemini' or 'mini'."
                : "The gateway has not cached any models here yet. Check back after the next refresh."
            }
            action={
              search ? (
                <Button variant="outline" size="sm" onClick={() => setSearch("")}>
                  Clear search
                </Button>
              ) : undefined
            }
          />
        ) : (
          <div className="grid gap-2.5">
            {models.map((model) => (
              <ModelRow
                key={model.model_id}
                model={model}
                accentScope={accent.scope}
                isSelected={model.model_id === selectedModelId}
                onSelect={() => handleSelectModel(model.model_id)}
                isBusy={isMutating}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function ModelRow({
  model,
  accentScope,
  isSelected,
  onSelect,
  isBusy,
}: {
  model: ModelInfo;
  accentScope: string;
  isSelected: boolean;
  onSelect: () => void;
  isBusy: boolean;
}) {
  const shortName = model.model_id.split("/").pop() || model.model_id;
  const vendor = model.model_id.includes("/") ? model.model_id.split("/")[0] : null;
  const prompt = model.pricing ? parseFloat(model.pricing.prompt) * 1_000_000 : null;
  const completion = model.pricing
    ? parseFloat(model.pricing.completion) * 1_000_000
    : null;

  return (
    <Card
      rail={isSelected}
      className={cn(
        accentScope,
        "px-4 py-3.5 transition-colors duration-200 ease-swift",
        isSelected ? "accent-border accent-tint" : "hover:border-line-strong"
      )}
    >
      <div className="flex flex-wrap items-center gap-x-4 gap-y-3">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="truncate text-base font-semibold text-ink">
              {model.model_name || shortName}
            </h3>
            {model.rank != null && model.rank <= 3 && (
              <Badge variant="outline">#{model.rank}</Badge>
            )}
            {model.is_curated && (
              <Badge variant="accent" dot>
                <Star className="h-3 w-3" aria-hidden />
                Curated
              </Badge>
            )}
            {isSelected && (
              <Badge variant="accent" dot>
                In use
              </Badge>
            )}
          </div>
          <p className="mt-0.5 truncate font-mono text-xs text-subtle">
            {vendor && <span className="text-muted">{vendor}/</span>}
            {shortName}
          </p>
        </div>

        <dl className="metric flex items-center gap-5 text-xs">
          <div>
            <dt className="text-subtle">Context</dt>
            <dd className="mt-0.5 font-medium text-ink">
              {formatNumber(model.context_length)}
            </dd>
          </div>
          {prompt != null && completion != null && (
            <>
              <div>
                <dt className="text-subtle">In / M</dt>
                <dd className="mt-0.5 font-medium text-ink">
                  {formatCurrency(prompt, 2)}
                </dd>
              </div>
              <div>
                <dt className="text-subtle">Out / M</dt>
                <dd className="mt-0.5 font-medium text-ink">
                  {formatCurrency(completion, 2)}
                </dd>
              </div>
            </>
          )}
        </dl>

        <Button
          variant={isSelected ? "secondary" : "outline"}
          size="sm"
          onClick={onSelect}
          disabled={isSelected || isBusy}
          isLoading={isBusy && !isSelected}
        >
          {isSelected ? (
            <>
              <Check className="h-3.5 w-3.5" aria-hidden />
              Selected
            </>
          ) : (
            "Use this"
          )}
        </Button>
      </div>
    </Card>
  );
}
