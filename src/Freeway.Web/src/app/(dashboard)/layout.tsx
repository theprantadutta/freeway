"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/lib/stores/auth-store";
import { Sidebar } from "@/components/layout/sidebar";
import { MobileNav } from "@/components/layout/mobile-nav";
import { ToastProvider } from "@/components/ui/toast";
import { LaneMark } from "@/components/brand/lane-mark";

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

    if (expiresAt && new Date(expiresAt) < new Date()) {
      useAuthStore.getState().clearAuth();
      router.push("/login");
      return;
    }

    if (!isAuthenticated) {
      router.push("/login");
    }
  }, [isAuthenticated, expiresAt, router, hasHydrated]);

  if (!hasHydrated) {
    return (
      <div className="grid h-screen place-items-center bg-surface">
        <div className="flex flex-col items-center gap-3">
          <span className="h-8 w-8 animate-pulse">
            <LaneMark />
          </span>
          <p className="text-sm text-subtle">Loading your gateway</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) return null;

  return (
    <ToastProvider>
      <div className="flex h-screen bg-surface">
        <Sidebar />
        <main className="flex-1 overflow-y-auto pb-[4.5rem] md:pb-0">
          {children}
        </main>
        <MobileNav />
      </div>
    </ToastProvider>
  );
}
