"use client";

import { useEffect, useLayoutEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  Coins,
  FolderKanban,
  LayoutDashboard,
  LogOut,
  Menu as MenuIcon,
  Monitor,
  Moon,
  Route,
  Settings2,
  Sun,
  X,
} from "lucide-react";
import { Mark } from "@/components/brand/mark";
import { useAuthStore } from "@/lib/stores/auth-store";
import { useThemeStore } from "@/lib/stores/theme-store";
import { creditsApi } from "@/lib/api/credits";
import { analyticsApi } from "@/lib/api/analytics";
import { cn } from "@/lib/utils/cn";
import { money } from "@/lib/utils/format";

const NAV = [
  { href: "/", label: "Overview", icon: LayoutDashboard },
  { href: "/projects", label: "Projects", icon: FolderKanban },
  { href: "/models", label: "Routing", icon: Route },
  { href: "/credits", label: "Credits", icon: Coins },
  { href: "/settings", label: "Settings", icon: Settings2 },
];

function isActive(pathname: string, href: string) {
  return pathname === href || (href !== "/" && pathname.startsWith(href));
}

/**
 * The console's spine. A side rail this time, with full labels and big targets:
 * the top bar it replaces hid five destinations in a strip of small grey words.
 * Below the navigation sits the state of the three routes every paid request
 * travels, so whether the free routes are up is visible from every page.
 */
export function Sidebar() {
  return (
    <aside className="sticky top-0 hidden h-screen w-[17rem] shrink-0 flex-col border-r border-hair/70 bg-sunken/45 backdrop-blur-xl lg:flex">
      <SidebarBody />
    </aside>
  );
}

function SidebarBody({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <div className="flex h-full flex-col">
      <Link href="/" onClick={onNavigate} className="flex items-center gap-3 px-6 pb-7 pt-7" aria-label="Freeway overview">
        <span className="h-9 w-9 shrink-0">
          <Mark />
        </span>
        <span className="text-xl font-extrabold tracking-tight text-text">Freeway</span>
      </Link>

      <Nav onNavigate={onNavigate} />

      <RouteStatus onNavigate={onNavigate} />

      <div className="mt-auto space-y-3 border-t border-hair px-4 py-4">
        <ThemeSwitch />
        <Account />
      </div>
    </div>
  );
}

function Nav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  const list = useRef<HTMLUListElement>(null);
  const [marker, setMarker] = useState<{ top: number; height: number } | null>(null);

  // The marker slides between items rather than blinking from one to the next.
  useLayoutEffect(() => {
    const el = list.current?.querySelector<HTMLElement>('[aria-current="page"]');
    if (!el) {
      setMarker(null);
      return;
    }
    setMarker({ top: el.offsetTop, height: el.offsetHeight });
  }, [pathname]);

  return (
    <nav aria-label="Main" className="px-3">
      <ul ref={list} className="relative space-y-1">
        {marker && (
          <span
            aria-hidden
            className="nav-active absolute left-0 right-0 rounded-xl transition-[top,height] duration-300 ease-out"
            style={{ top: marker.top, height: marker.height }}
          >
            <span className="stripe absolute left-2 top-1/2 h-6 w-1 -translate-y-1/2 rounded-full" />
          </span>
        )}
        {NAV.map((item) => {
          const active = isActive(pathname, item.href);
          return (
            <li key={item.href}>
              <Link
                href={item.href}
                onClick={onNavigate}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "relative z-10 flex h-12 items-center gap-3.5 rounded-xl pl-6 pr-3 text-base font-semibold",
                  "transition-colors duration-200",
                  active ? "text-text" : "text-text-3 hover:bg-raised/50 hover:text-text-2"
                )}
              >
                <item.icon className="h-5 w-5 shrink-0" aria-hidden />
                {item.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

/** The three routes a paid request tries, as they stand right now. */
function RouteStatus({ onNavigate }: { onNavigate?: () => void }) {
  const credit = useQuery({ queryKey: ["credits"], queryFn: creditsApi.getOverview, refetchInterval: 60_000 });
  const bridge = useQuery({ queryKey: ["local-claude"], queryFn: analyticsApi.getLocalClaude, refetchInterval: 60_000 });

  const c = credit.data;
  const b = bridge.data;

  const rows = [
    {
      label: "Claude credit",
      state: !c ? "loading" : !c.configured ? "off" : c.can_serve ? "up" : "down",
      value: c?.configured ? `${money(c.remaining_usd)} left` : c ? "Off" : "…",
      href: "/credits",
    },
    {
      label: "Claude Code",
      state: !b ? "loading" : !b.configured ? "off" : b.can_serve ? "up" : "down",
      value: !b
        ? "…"
        : !b.configured
          ? "Off"
          : b.plan?.utilization_percent != null
            ? `${Math.round(b.plan.utilization_percent)}% of plan`
            : b.can_serve
              ? "Ready"
              : "Unavailable",
      href: "/settings",
    },
    { label: "OpenRouter", state: "standby", value: "Billed fallback", href: "/models" },
  ] as const;

  return (
    <div className="card mx-3 mt-8 rounded-2xl p-4">
      <p className="mb-3 text-sm font-bold text-text-2">Routes</p>
      <ul className="space-y-3">
        {rows.map((r) => (
          <li key={r.label}>
            <Link href={r.href} onClick={onNavigate} className="group flex items-center gap-3 rounded-lg">
              <span
                className={cn(
                  "h-2 w-2 shrink-0 rounded-full",
                  r.state === "up" && "live-dot bg-ok text-ok",
                  r.state === "down" && "bg-bad",
                  r.state === "off" && "bg-text-3/50",
                  r.state === "standby" && "bg-text-3",
                  r.state === "loading" && "bg-hair-bright"
                )}
                aria-hidden
              />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-semibold text-text group-hover:underline">
                  {r.label}
                </span>
                <span className="block truncate text-xs text-text-3">{r.value}</span>
              </span>
              <span className="sr-only">
                {r.state === "up" ? "available" : r.state === "down" ? "not available" : r.state}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}

function ThemeSwitch() {
  const { theme, setTheme } = useThemeStore();
  const options = [
    { value: "dark", icon: Moon, label: "Dark" },
    { value: "light", icon: Sun, label: "Light" },
    { value: "system", icon: Monitor, label: "System" },
  ] as const;

  return (
    <div role="radiogroup" aria-label="Theme" className="grid grid-cols-3 gap-1 rounded-xl bg-page/60 p-1 ring-1 ring-hair">
      {options.map((o) => (
        <button
          key={o.value}
          role="radio"
          aria-checked={theme === o.value}
          onClick={() => setTheme(o.value)}
          className={cn(
            "flex h-8 items-center justify-center gap-1.5 rounded-lg text-xs font-semibold transition-colors duration-200",
            theme === o.value ? "bg-raised text-text ring-1 ring-hair-bright" : "text-text-3 hover:text-text-2"
          )}
        >
          <o.icon className="h-3.5 w-3.5" aria-hidden />
          {o.label}
        </button>
      ))}
    </div>
  );
}

function Account() {
  const user = useAuthStore((s) => s.user);
  const clearAuth = useAuthStore((s) => s.clearAuth);
  const name = user?.name || user?.email?.split("@")[0] || "Signed in";

  return (
    <div className="flex items-center gap-3 rounded-xl px-1">
      <span className="grad grid h-9 w-9 shrink-0 place-items-center rounded-full text-sm font-bold text-white">
        {name.charAt(0).toUpperCase()}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-semibold text-text">{name}</span>
        <span className="block truncate text-xs text-text-3">{user?.email}</span>
      </span>
      <button
        onClick={() => {
          clearAuth();
          window.location.href = "/login";
        }}
        aria-label="Sign out"
        title="Sign out"
        className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-text-3 transition-colors hover:bg-raised hover:text-text"
      >
        <LogOut className="h-4 w-4" aria-hidden />
      </button>
    </div>
  );
}

/** Below lg: a slim bar with the mark and a button that opens the rail as a drawer. */
export function MobileBar() {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();

  useEffect(() => setOpen(false), [pathname]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("keydown", onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = prev;
    };
  }, [open]);

  const current = NAV.find((n) => isActive(pathname, n.href));

  return (
    <>
      <header className="sticky top-0 z-40 flex h-16 items-center gap-3 border-b border-hair bg-page/85 px-4 backdrop-blur-xl lg:hidden">
        <Link href="/" className="flex items-center gap-2.5" aria-label="Freeway overview">
          <span className="h-8 w-8">
            <Mark />
          </span>
          <span className="text-lg font-extrabold tracking-tight text-text">Freeway</span>
        </Link>
        <span className="ml-1 truncate text-base text-text-3">{current?.label}</span>
        <button
          onClick={() => setOpen(true)}
          aria-label="Open navigation"
          aria-expanded={open}
          className="ml-auto grid h-10 w-10 place-items-center rounded-xl text-text-2 ring-1 ring-hair transition-colors hover:bg-raised"
        >
          <MenuIcon className="h-5 w-5" aria-hidden />
        </button>
      </header>

      {open && (
        <div className="fixed inset-0 z-50 lg:hidden">
          <div className="absolute inset-0 bg-[rgb(5_9_18/0.62)] backdrop-blur-[2px] animate-fade" onClick={() => setOpen(false)} aria-hidden />
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Navigation"
            className="absolute inset-y-0 left-0 w-[19rem] max-w-[86vw] overflow-y-auto border-r border-hair bg-page shadow-overlay"
            style={{ animation: "slide-in 320ms cubic-bezier(0.16,1,0.3,1) both" }}
          >
            <button
              onClick={() => setOpen(false)}
              aria-label="Close navigation"
              className="absolute right-3 top-6 z-10 grid h-9 w-9 place-items-center rounded-lg text-text-3 hover:bg-raised hover:text-text"
            >
              <X className="h-5 w-5" aria-hidden />
            </button>
            <SidebarBody onNavigate={() => setOpen(false)} />
          </div>
          <style>{`@keyframes slide-in{from{transform:translateX(-100%)}to{transform:none}}`}</style>
        </div>
      )}
    </>
  );
}
