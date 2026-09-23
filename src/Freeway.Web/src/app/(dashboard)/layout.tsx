"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/lib/stores/auth-store";
import { TopBar } from "@/components/shell/top-bar";
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
      <div className="grid min-h-screen place-items-center bg-base">
        <span className="h-9 w-9 animate-pulse">
          <Mark />
        </span>
      </div>
    );
  }

  if (!isAuthenticated) return null;

  return (
    <ToastProvider>
      <div className="min-h-screen bg-base">
        <TopBar />
        <main className="mx-auto max-w-[1600px] px-4 py-7 sm:px-6">{children}</main>
      </div>
    </ToastProvider>
  );
}
