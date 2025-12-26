"use client";

import { useQuery } from "@tanstack/react-query";
import {
  FolderKanban,
  Activity,
  DollarSign,
  Zap,
  TrendingUp,
  Brain,
} from "lucide-react";
import { Header } from "@/components/layout/header";
import { Card, CardContent } from "@/components/ui/card";
import { StatCard } from "@/components/ui/stat-card";
import { Badge } from "@/components/ui/badge";
import { Skeleton, SkeletonCard } from "@/components/ui/skeleton";
import { analyticsApi } from "@/lib/api/analytics";
import { modelsApi } from "@/lib/api/models";
import { formatCurrency, formatNumber } from "@/lib/utils/format";

export default function DashboardPage() {
  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ["analytics", "summary"],
    queryFn: () => analyticsApi.getGlobalSummary(),
  });

  const { data: freeModel, isLoading: freeModelLoading } = useQuery({
    queryKey: ["model", "free"],
    queryFn: () => modelsApi.getSelectedFreeModel(),
  });

  const { data: paidModel, isLoading: paidModelLoading } = useQuery({
    queryKey: ["model", "paid"],
    queryFn: () => modelsApi.getSelectedPaidModel(),
  });

  return (
    <div className="flex flex-col h-full">
      <Header title="Dashboard" subtitle="Overview of your AI gateway" />

      <div className="flex-1 p-4 md:p-6 space-y-6">
        {/* Stats Grid */}
        <section>
          <h2 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
            Overview
          </h2>
          {summaryLoading ? (
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
              {[...Array(4)].map((_, i) => (
                <Skeleton key={i} className="h-24" />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
              <StatCard
                title="Total Projects"
                value={summary?.total_projects || 0}
                icon={FolderKanban}
                iconColor="text-blue-500"
              />
              <StatCard
                title="Active Projects"
                value={summary?.active_projects || 0}
                icon={Activity}
                iconColor="text-green-500"
              />
              <StatCard
                title="Requests Today"
                value={formatNumber(summary?.requests_today || 0)}
                icon={Zap}
                iconColor="text-amber-500"
              />
              <StatCard
                title="Cost This Month"
                value={formatCurrency(summary?.total_cost_this_month || 0)}
                icon={DollarSign}
                iconColor="text-purple-500"
              />
            </div>
          )}
        </section>

        {/* Additional Stats */}
        {summary && (
          <section>
            <div className="grid grid-cols-2 gap-4">
              <StatCard
                title="Requests This Month"
                value={formatNumber(summary.requests_this_month)}
                icon={TrendingUp}
                iconColor="text-indigo-500"
              />
              <StatCard
                title="Cost Today"
                value={formatCurrency(summary.total_cost_today, 6)}
                icon={DollarSign}
                iconColor="text-rose-500"
              />
            </div>
          </section>
        )}

        {/* Selected Models */}
        <section>
          <h2 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
            Selected Models
          </h2>
          <div className="grid md:grid-cols-2 gap-4">
            {/* Free Model */}
            {freeModelLoading ? (
              <SkeletonCard />
            ) : (
              <Card>
                <CardContent className="pt-6">
                  <div className="flex items-start gap-4">
                    <div className="p-3 bg-green-100 dark:bg-green-900/30 rounded-lg">
                      <Brain className="h-6 w-6 text-green-600 dark:text-green-400" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <h3 className="font-medium text-gray-900 dark:text-gray-100">
                          Free Model
                        </h3>
                        <Badge variant="success">Active</Badge>
                      </div>
                      <p className="text-sm text-gray-500 dark:text-gray-400 truncate font-mono">
                        {freeModel?.model_id || "Not configured"}
                      </p>
                      {freeModel?.context_length && (
                        <p className="text-xs text-gray-400 dark:text-gray-500 mt-1">
                          {formatNumber(freeModel.context_length)} context
                        </p>
                      )}
                    </div>
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Paid Model */}
            {paidModelLoading ? (
              <SkeletonCard />
            ) : (
              <Card>
                <CardContent className="pt-6">
                  <div className="flex items-start gap-4">
                    <div className="p-3 bg-purple-100 dark:bg-purple-900/30 rounded-lg">
                      <Brain className="h-6 w-6 text-purple-600 dark:text-purple-400" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <h3 className="font-medium text-gray-900 dark:text-gray-100">
                          Paid Model
                        </h3>
                        <Badge variant="primary">Active</Badge>
                      </div>
                      <p className="text-sm text-gray-500 dark:text-gray-400 truncate font-mono">
                        {paidModel?.model_id || "Not configured"}
                      </p>
                      {paidModel?.context_length && (
                        <p className="text-xs text-gray-400 dark:text-gray-500 mt-1">
                          {formatNumber(paidModel.context_length)} context
                        </p>
                      )}
                    </div>
                  </div>
                </CardContent>
              </Card>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
