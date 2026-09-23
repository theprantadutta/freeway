"use client";

import { useState, useMemo } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Search, Brain, Check, Sparkles, Image, Star } from "lucide-react";
import { Header } from "@/components/layout/header";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { modelsApi } from "@/lib/api/models";
import { formatNumber, formatCurrency } from "@/lib/utils/format";
import { cn } from "@/lib/utils/cn";
import {
  PAID_TIERS,
  PAID_TIER_LABELS,
  type ModelInfo,
  type PaidTier,
} from "@/lib/types";

const TIER_HINTS: Record<PaidTier, string> = {
  low: "Cheapest available models. This is what a plain `paid` request uses.",
  moderate: "Balanced cost and capability. Curated mid-range models first.",
  premium: "Highest capability. Curated frontier models first — the most expensive tier.",
};

export default function ModelsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState("");
  const [activeTab, setActiveTab] = useState("free");
  const [paidTier, setPaidTier] = useState<PaidTier>("low");

  // Queries
  const { data: freeModels, isLoading: loadingFree } = useQuery({
    queryKey: ["models", "free"],
    queryFn: () => modelsApi.getFreeModels(),
  });

  const { data: paidModels, isLoading: loadingPaid } = useQuery({
    queryKey: ["models", "paid", paidTier],
    queryFn: () => modelsApi.getPaidModelsForTier(paidTier),
  });

  const { data: imageModels, isLoading: loadingImage } = useQuery({
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

  // Mutations
  const setFreeMutation = useMutation({
    mutationFn: (modelId: string) => modelsApi.setSelectedFreeModel(modelId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["model", "free"] });
      toast("Free model updated successfully", "success");
    },
    onError: () => {
      toast("Failed to update free model", "error");
    },
  });

  const setPaidMutation = useMutation({
    mutationFn: (modelId: string) =>
      modelsApi.setSelectedPaidModelForTier(paidTier, modelId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["model", "paid"] });
      toast(
        `${PAID_TIER_LABELS[paidTier]} paid model updated successfully`,
        "success"
      );
    },
    onError: () => {
      toast(`Failed to update ${PAID_TIER_LABELS[paidTier]} paid model`, "error");
    },
  });

  const setImageMutation = useMutation({
    mutationFn: (modelId: string) => modelsApi.setSelectedImageModel(modelId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["model", "image"] });
      toast("Image model updated successfully", "success");
    },
    onError: () => {
      toast("Failed to update image model", "error");
    },
  });

  // Filter models by search
  const filteredFree = useMemo(() => {
    if (!freeModels) return [];
    if (!search) return freeModels;
    const q = search.toLowerCase();
    return freeModels.filter(
      (m) =>
        m.model_id.toLowerCase().includes(q) ||
        m.model_name.toLowerCase().includes(q)
    );
  }, [freeModels, search]);

  const filteredPaid = useMemo(() => {
    if (!paidModels) return [];
    if (!search) return paidModels;
    const q = search.toLowerCase();
    return paidModels.filter(
      (m) =>
        m.model_id.toLowerCase().includes(q) ||
        m.model_name.toLowerCase().includes(q)
    );
  }, [paidModels, search]);

  const filteredImage = useMemo(() => {
    if (!imageModels) return [];
    if (!search) return imageModels;
    const q = search.toLowerCase();
    return imageModels.filter(
      (m) =>
        m.model_id.toLowerCase().includes(q) ||
        m.model_name.toLowerCase().includes(q)
    );
  }, [imageModels, search]);

  const handleSelectModel = (modelId: string) => {
    if (activeTab === "free") {
      setFreeMutation.mutate(modelId);
    } else if (activeTab === "paid") {
      setPaidMutation.mutate(modelId);
    } else {
      setImageMutation.mutate(modelId);
    }
  };

  const isLoading =
    activeTab === "free"
      ? loadingFree
      : activeTab === "paid"
        ? loadingPaid
        : loadingImage;
  const models =
    activeTab === "free"
      ? filteredFree
      : activeTab === "paid"
        ? filteredPaid
        : filteredImage;
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

  return (
    <div className="flex flex-col h-full">
      <Header title="Models" subtitle="Manage AI model selection" />

      <div className="flex-1 p-4 md:p-6 space-y-4">
        {/* Search and Tabs */}
        <div className="flex flex-col sm:flex-row gap-4">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
            <Input
              placeholder="Search models..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-9"
            />
          </div>
          <Tabs
            defaultValue="free"
            onChange={(v) => setActiveTab(v)}
            className="w-full sm:w-auto"
          >
            <TabsList className="w-full sm:w-auto">
              <TabsTrigger value="free">Free</TabsTrigger>
              <TabsTrigger value="paid">Paid</TabsTrigger>
              <TabsTrigger value="image">Image</TabsTrigger>
            </TabsList>
          </Tabs>
        </div>

        {/* Paid tier selector */}
        {activeTab === "paid" && (
          <div className="space-y-2">
            <div
              role="tablist"
              aria-label="Paid tier"
              className="inline-flex gap-1 p-1 bg-gray-100 dark:bg-gray-800 rounded-lg"
            >
              {PAID_TIERS.map((tier) => (
                <button
                  key={tier}
                  role="tab"
                  aria-selected={paidTier === tier}
                  onClick={() => setPaidTier(tier)}
                  className={cn(
                    "px-4 py-1.5 text-sm font-medium rounded-md transition-colors",
                    paidTier === tier
                      ? "bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100 shadow-sm"
                      : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-200"
                  )}
                >
                  {PAID_TIER_LABELS[tier]}
                </button>
              ))}
            </div>
            <p className="text-xs text-gray-500 dark:text-gray-400">
              {TIER_HINTS[paidTier]} Request it with{" "}
              <code className="font-mono text-gray-700 dark:text-gray-300">
                &quot;model&quot;: &quot;paid:{paidTier}&quot;
              </code>
              .
            </p>
          </div>
        )}

        {/* Models List */}
        {isLoading ? (
          <SkeletonList count={5} />
        ) : models.length === 0 ? (
          <EmptyState
            icon={Brain}
            title="No models found"
            description={
              search
                ? "Try a different search term"
                : "No models available in this category"
            }
          />
        ) : (
          <div className="grid gap-4">
            {models.map((model) => (
              <ModelCard
                key={model.model_id}
                model={model}
                isSelected={model.model_id === selectedModelId}
                onSelect={() => handleSelectModel(model.model_id)}
                isLoading={isMutating}
                type={activeTab as "free" | "paid" | "image"}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

interface ModelCardProps {
  model: ModelInfo;
  isSelected: boolean;
  onSelect: () => void;
  isLoading: boolean;
  type: "free" | "paid" | "image";
}

function ModelCard({
  model,
  isSelected,
  onSelect,
  isLoading,
  type,
}: ModelCardProps) {
  const shortName = model.model_id.split("/").pop() || model.model_id;

  return (
    <Card hover className={isSelected ? "ring-2 ring-primary-500" : ""}>
      <CardContent className="py-4">
        <div className="flex items-start gap-4">
          <div
            className={`p-2.5 rounded-lg ${
              type === "free"
                ? "bg-green-100 dark:bg-green-900/30"
                : type === "paid"
                  ? "bg-purple-100 dark:bg-purple-900/30"
                  : "bg-amber-100 dark:bg-amber-900/30"
            }`}
          >
            {type === "image" ? (
              <Image
                className="h-5 w-5 text-amber-600 dark:text-amber-400"
              />
            ) : (
              <Brain
                className={`h-5 w-5 ${
                  type === "free"
                    ? "text-green-600 dark:text-green-400"
                    : "text-purple-600 dark:text-purple-400"
                }`}
              />
            )}
          </div>

          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="font-medium text-gray-900 dark:text-gray-100">
                {model.model_name || shortName}
              </h3>
              {isSelected && (
                <Badge variant="success">
                  <Check className="h-3 w-3 mr-1" />
                  Active
                </Badge>
              )}
              {model.is_curated && (
                <Badge variant="info" title="Preferred model for this tier">
                  <Star className="h-3 w-3 mr-1" />
                  Curated
                </Badge>
              )}
            </div>

            <p className="text-sm text-gray-500 dark:text-gray-400 font-mono truncate mt-0.5">
              {model.model_id}
            </p>

            <div className="flex flex-wrap gap-4 mt-2 text-xs text-gray-500 dark:text-gray-400">
              <span>Context: {formatNumber(model.context_length)}</span>
              {model.pricing && (
                <>
                  <span>
                    Input: {formatCurrency(parseFloat(model.pricing.prompt) * 1000000, 2)}/M
                  </span>
                  <span>
                    Output:{" "}
                    {formatCurrency(parseFloat(model.pricing.completion) * 1000000, 2)}/M
                  </span>
                </>
              )}
            </div>
          </div>

          <Button
            variant={isSelected ? "secondary" : "primary"}
            size="sm"
            onClick={onSelect}
            disabled={isSelected || isLoading}
            isLoading={isLoading && !isSelected}
          >
            {isSelected ? (
              <>
                <Check className="h-4 w-4 mr-1" />
                Selected
              </>
            ) : (
              <>
                <Sparkles className="h-4 w-4 mr-1" />
                Select
              </>
            )}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
