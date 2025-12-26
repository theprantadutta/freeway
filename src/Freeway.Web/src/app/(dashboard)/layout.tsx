"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/lib/stores/auth-store";
import { Sidebar } from "@/components/layout/sidebar";
import { MobileNav } from "@/components/layout/mobile-nav";
import { ToastProvider } from "@/components/ui/toast";
import { Zap } from "lucide-react";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const hasHydrated = useAuthStore((state) => state._hasHydrated);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const expiresAt = useAuthStore((state) => state.expiresAt);

  useEffect(() => {
    if (!hasHydrated) return;

    // Check if token is expired
    if (expiresAt && new Date(expiresAt) < new Date()) {
      useAuthStore.getState().clearAuth();
      router.push("/login");
      return;
    }

    if (!isAuthenticated) {
      router.push("/login");
    }
  }, [isAuthenticated, expiresAt, router, hasHydrated]);

  // Show loading while hydrating
  if (!hasHydrated) {
    return (
      <div className="flex h-screen items-center justify-center bg-gray-50 dark:bg-gray-950">
        <div className="flex flex-col items-center gap-4">
          <div className="p-3 bg-primary-100 dark:bg-primary-900/30 rounded-xl animate-pulse">
            <Zap className="h-8 w-8 text-primary-600 dark:text-primary-400" />
          </div>
          <p className="text-sm text-gray-500 dark:text-gray-400">Loading...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  return (
    <ToastProvider>
      <div className="flex h-screen bg-gray-50 dark:bg-gray-950">
        <Sidebar />
        <main className="flex-1 overflow-y-auto pb-16 md:pb-0">{children}</main>
        <MobileNav />
      </div>
    </ToastProvider>
  );
}
