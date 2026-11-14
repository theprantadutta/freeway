"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/lib/stores/auth-store";
import { MobileBar, Sidebar } from "@/components/shell/sidebar";
import { ToastProvider } from "@/components/ui/toast";
import { Mark } from "@/components/brand/mark";

export default function ConsoleLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const hydrated = useAuthStore((s) => s._hasHydrated);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const expiresAt = useAuthStore((s) => s.expiresAt);

  useEffect(() => {
    if (!hydrated) return;

    if (expiresAt && new Date(expiresAt) < new Date()) {
      useAuthStore.getState().clearAuth();
      router.push("/login");
      return;
    }

    if (!isAuthenticated) router.push("/login");
  }, [hydrated, isAuthenticated, expiresAt, router]);

  if (!hydrated) {
    return (
      <div className="grid min-h-screen place-items-center bg-page">
        <span className="h-10 w-10 animate-pulse">
          <Mark />
        </span>
      </div>
    );
  }

  if (!isAuthenticated) return null;

  return (
    <ToastProvider>
      <div className="flex min-h-screen bg-page">
        <Sidebar />
        <div className="min-w-0 flex-1">
          <MobileBar />
          <main className="mx-auto w-full max-w-[1520px] px-4 pb-16 pt-8 sm:px-6 lg:px-10 lg:pt-10">
            {children}
          </main>
        </div>
      </div>
    </ToastProvider>
  );
}
