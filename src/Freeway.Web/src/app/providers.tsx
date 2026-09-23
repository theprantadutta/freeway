"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useThemeStore } from "@/lib/stores/theme-store";

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 60 * 1000,
            retry: 1,
          },
        },
      })
  );

  const theme = useThemeStore((state) => state.theme);

  useEffect(() => {
    const root = document.documentElement;

    // The console is dark by default and `.light` opts out of it, so that is the
    // class to drive. This used to toggle `.dark`, which no rule matched, leaving
    // the switch inert.
    const apply = (dark: boolean) => root.classList.toggle("light", !dark);

    if (theme !== "system") {
      apply(theme === "dark");
      return;
    }

    // Following the system means following it as it changes, not just reading it
    // once when the page loads.
    const query = window.matchMedia("(prefers-color-scheme: dark)");
    apply(query.matches);

    const onChange = (e: MediaQueryListEvent) => apply(e.matches);
    query.addEventListener("change", onChange);
    return () => query.removeEventListener("change", onChange);
  }, [theme]);

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
