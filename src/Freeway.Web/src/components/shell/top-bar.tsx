"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { ChevronDown, LogOut, Monitor, Moon, Sun } from "lucide-react";
import { Mark } from "@/components/brand/mark";
import { useAuthStore } from "@/lib/stores/auth-store";
import { useThemeStore } from "@/lib/stores/theme-store";
import { cn } from "@/lib/utils/cn";

const NAV = [
  { href: "/", label: "Overview" },
  { href: "/models", label: "Models" },
  { href: "/projects", label: "Projects" },
  { href: "/settings", label: "Settings" },
];

/**
 * The console header.
 *
 * A top bar rather than a side rail: this product is about a wide, dense board of
 * figures, and a fixed 240px column of mostly-empty navigation was taking width the
 * data needed. Navigation is four links; it does not deserve a whole edge of the
 * screen.
 */
export function TopBar() {
  const pathname = usePathname();

  return (
    <header className="sticky top-0 z-40 border-b border-hair bg-base/85 backdrop-blur-xl">
      <div className="ticks h-px w-full opacity-40" aria-hidden />
      <div className="mx-auto flex h-14 max-w-[1600px] items-center gap-6 px-4 sm:px-6">
        <Link href="/" className="flex shrink-0 items-center gap-2.5 rounded" aria-label="Freeway">
          <span className="h-7 w-7">
            <Mark />
          </span>
          <span className="hidden text-base font-semibold tracking-tight text-text sm:block">
            Freeway
          </span>
        </Link>

        <nav className="flex min-w-0 flex-1 items-center gap-0.5 overflow-x-auto">
          {NAV.map((item) => {
            const active =
              pathname === item.href ||
              (item.href !== "/" && pathname.startsWith(item.href));
            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "relative shrink-0 rounded px-3 py-1.5 text-sm font-medium transition-colors duration-150 ease-out",
                  active ? "text-text" : "text-text-3 hover:text-text-2"
                )}
              >
                {item.label}
                {active && (
                  <span
                    className="absolute inset-x-3 -bottom-[13px] h-0.5 rounded-full bg-accent"
                    aria-hidden
                  />
                )}
              </Link>
            );
          })}
        </nav>

        <div className="flex shrink-0 items-center gap-2">
          <ThemeToggle />
          <AccountMenu />
        </div>
      </div>
    </header>
  );
}

function ThemeToggle() {
  const { theme, setTheme } = useThemeStore();
  const options = [
    { value: "dark", icon: Moon, label: "Dark" },
    { value: "light", icon: Sun, label: "Light" },
    { value: "system", icon: Monitor, label: "System" },
  ] as const;

  return (
    <div
      role="radiogroup"
      aria-label="Theme"
      className="hidden items-center gap-0.5 rounded bg-sunken p-0.5 sm:flex"
    >
      {options.map((o) => (
        <button
          key={o.value}
          role="radio"
          aria-checked={theme === o.value}
          aria-label={o.label}
          title={o.label}
          onClick={() => setTheme(o.value)}
          className={cn(
            "grid h-6 w-6 place-items-center rounded-[0.25rem] transition-colors duration-150",
            theme === o.value ? "bg-raised text-text" : "text-text-3 hover:text-text-2"
          )}
        >
          <o.icon className="h-3.5 w-3.5" aria-hidden />
        </button>
      ))}
    </div>
  );
}

function AccountMenu() {
  const user = useAuthStore((s) => s.user);
  const clearAuth = useAuthStore((s) => s.clearAuth);
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);

  const initial = (user?.name || user?.email || "?").charAt(0).toUpperCase();

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-haspopup="menu"
        className="flex items-center gap-1.5 rounded py-1 pl-1 pr-1.5 transition-colors hover:bg-raised"
      >
        <span className="grid h-6 w-6 place-items-center rounded bg-accent/15 text-2xs font-semibold text-accent">
          {initial}
        </span>
        <ChevronDown className="h-3.5 w-3.5 text-text-3" aria-hidden />
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} aria-hidden />
          <div
            role="menu"
            className="absolute right-0 top-full z-20 mt-1.5 w-56 overflow-hidden rounded border border-hair-bright bg-raised animate-pop"
          >
            <div className="border-b border-hair px-3 py-2.5">
              <p className="truncate text-sm font-medium text-text">
                {user?.name || user?.email?.split("@")[0]}
              </p>
              <p className="truncate text-xs text-text-3">{user?.email}</p>
            </div>
            <button
              role="menuitem"
              onClick={() => {
                clearAuth();
                window.location.href = "/login";
              }}
              className="flex w-full items-center gap-2.5 px-3 py-2.5 text-sm text-text-2 transition-colors hover:bg-panel hover:text-bad"
            >
              <LogOut className="h-3.5 w-3.5" aria-hidden />
              Sign out
            </button>
          </div>
        </>
      )}
    </div>
  );
}
