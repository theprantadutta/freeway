"use client";

import Link from "next/link";
import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { Activity, ArrowUpRight, DollarSign, FolderKanban, Zap } from "lucide-react";
import { Header } from "@/components/layout/header";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Stat, StatStrip } from "@/components/ui/stat-card";
import { Skeleton, SkeletonStrip } from "@/components/ui/skeleton";
import { ErrorState } from "@/components/ui/empty-state";
import { analyticsApi } from "@/lib/api/analytics";
import { modelsApi } from "@/lib/api/models";
import { formatCurrency, formatNumber } from "@/lib/utils/format";
import { ACCENTS, type AccentKey } from "@/lib/theme/accents";
import type { SelectedModel } from "@/lib/types";

export default function DashboardPage() {
  const summaryQuery = useQuery({
    queryKey: ["analytics", "summary"],
    queryFn: () => analyticsApi.getGlobalSummary(),
  });
  const summary = summaryQuery.data;

  const freeModel = useQuery({
    queryKey: ["model", "free"],
    queryFn: () => modelsApi.getSelectedFreeModel(),
  });

  const paidLow = useQuery({
    queryKey: ["model", "paid", "low"],
    queryFn: () => modelsApi.getSelectedPaidModelForTier("low"),
  });

  const paidModerate = useQuery({
    queryKey: ["model", "paid", "moderate"],
    queryFn: () => modelsApi.getSelectedPaidModelForTier("moderate"),
  });

  const paidPremium = useQuery({
    queryKey: ["model", "paid", "premium"],
    queryFn: () => modelsApi.getSelectedPaidModelForTier("premium"),
  });

  const imageModel = useQuery({
    queryKey: ["model", "image"],
    queryFn: () => modelsApi.getSelectedImageModel(),
  });

  // One lane per model type, in the order a request would escalate through them.
  const lanes: { key: AccentKey; query: UseQueryResult<SelectedModel> }[] = [
    { key: "free", query: freeModel },
    { key: "low", query: paidLow },
    { key: "moderate", query: paidModerate },
    { key: "premium", query: paidPremium },
    { key: "image", query: imageModel },
  ];

  return (
    <div className="flex h-full flex-col">
      <Header title="Dashboard" subtitle="What is flowing through the gateway" />

      <div className="flex-1 space-y-8 p-4 md:p-6">
        <section aria-labelledby="overview-heading">
          <h2 id="overview-heading" className="sr-only">
            Overview
          </h2>

          {summaryQuery.isLoading ? (
            <SkeletonStrip />
          ) : summaryQuery.isError ? (
            <ErrorState
              title="Could not load usage figures"
              description="The gateway did not return a summary. Everything else on this page still works."
              onRetry={() => summaryQuery.refetch()}
            />
          ) : (
            <StatStrip className="stagger">
              <Stat
                label="Active projects"
                value={summary?.active_projects ?? 0}
                icon={FolderKanban}
                accent={ACCENTS.free.scope}
                note={`${summary?.total_projects ?? 0} total`}
              />
              <Stat
                label="Requests today"
                value={formatNumber(summary?.requests_today ?? 0)}
                icon={Zap}
                accent={ACCENTS.low.scope}
                note={`${formatCurrency(summary?.total_cost_today ?? 0, 4)} spent`}
              />
              <Stat
                label="Requests this month"
                value={formatNumber(summary?.requests_this_month ?? 0)}
                icon={Activity}
                accent={ACCENTS.moderate.scope}
              />
              <Stat
                label="Cost this month"
                value={formatCurrency(summary?.total_cost_this_month ?? 0, 2)}
                icon={DollarSign}
                accent={ACCENTS.premium.scope}
                note="Billed by OpenRouter"
              />
            </StatStrip>
          )}
        </section>

        <section aria-labelledby="lanes-heading">
          <div className="mb-3 flex items-end justify-between gap-4">
            <div>
              <h2
                id="lanes-heading"
                className="text-base font-semibold tracking-tight text-ink"
              >
                Routing lanes
              </h2>
              <p className="text-sm text-muted">
                Where each kind of request goes right now
              </p>
            </div>
            <Link
              href="/models"
              className="inline-flex shrink-0 items-center gap-1 rounded-control text-sm font-medium text-brand hover:underline"
            >
              Change models
              <ArrowUpRight className="h-3.5 w-3.5" aria-hidden />
            </Link>
          </div>

          <div className="stagger grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {lanes.map(({ key, query }) => (
              <LaneCard key={key} accentKey={key} query={query} />
            ))}
          </div>
        </section>
      </div>
    </div>
  );
}

function LaneCard({
  accentKey,
  query,
}: {
  accentKey: AccentKey;
  query: UseQueryResult<SelectedModel>;
}) {
  const def = ACCENTS[accentKey];
  const Icon = def.icon;
  const model = query.data;

  const prompt = model?.pricing ? parseFloat(model.pricing.prompt) * 1_000_000 : null;
  const completion = model?.pricing
    ? parseFloat(model.pricing.completion) * 1_000_000
    : null;

  return (
    <Card rail hover className={`${def.scope} overflow-hidden`}>
      <div className="accent-wash px-5 pb-4 pt-4">
        <div className="flex items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2.5">
            <span className="grid h-8 w-8 shrink-0 place-items-center rounded-control accent-tint-strong accent-text">
              <Icon className="h-4 w-4" aria-hidden />
            </span>
            <h3 className="truncate text-base font-semibold text-ink">{def.label}</h3>
          </div>
          {def.model && (
            <Badge variant="accent" size="md" className="font-mono">
              {def.model}
            </Badge>
          )}
        </div>

        <div className="mt-4 min-h-[2.75rem]">
          {query.isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-4 w-4/5" />
              <Skeleton className="h-3 w-2/5" />
            </div>
          ) : model?.model_id ? (
            <>
              <p className="truncate font-mono text-sm text-ink" title={model.model_id}>
                {model.model_id}
              </p>
              {prompt != null && completion != null ? (
                <p className="metric mt-1 text-xs text-muted">
                  {formatCurrency(prompt, 2)} in
                  <span className="mx-1.5 text-subtle">/</span>
                  {formatCurrency(completion, 2)} out
                  <span className="text-subtle"> per M tokens</span>
                </p>
              ) : (
                <p className="mt-1 text-xs text-ok">No cost</p>
              )}
            </>
          ) : (
            <p className="text-sm text-subtle">
              No model selected. Pick one on the Models page.
            </p>
          )}
        </div>
      </div>

      {def.hint && (
        <p className="border-t border-line px-5 py-3 text-xs leading-relaxed text-muted">
          {def.hint}
        </p>
      )}
    </Card>
  );
}
