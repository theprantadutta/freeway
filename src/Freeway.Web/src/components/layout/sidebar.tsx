"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Brain,
  FolderKanban,
  Settings,
  LogOut,
  PanelLeftClose,
  PanelLeftOpen,
} from "lucide-react";
import { useState } from "react";
import { cn } from "@/lib/utils/cn";
import { useAuthStore } from "@/lib/stores/auth-store";
import { LaneMark } from "@/components/brand/lane-mark";

const navItems = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard },
  { href: "/models", label: "Models", icon: Brain },
  { href: "/projects", label: "Projects", icon: FolderKanban },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function Sidebar() {
  const pathname = usePathname();
  const user = useAuthStore((state) => state.user);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const [isCollapsed, setIsCollapsed] = useState(false);

  const handleLogout = () => {
    clearAuth();
    window.location.href = "/login";
  };

  return (
    <aside
      className={cn(
        "hidden h-screen shrink-0 flex-col border-r border-line bg-panel md:flex",
        "transition-[width] duration-300 ease-swift",
        isCollapsed ? "w-[68px]" : "w-60"
      )}
    >
      <div className="flex h-14 items-center px-4">
        <Link
          href="/"
          className="flex items-center gap-2.5 rounded-control"
          aria-label="Freeway home"
        >
          <span className="h-7 w-7 shrink-0">
            <LaneMark />
          </span>
          {!isCollapsed && (
            <span className="text-base font-semibold tracking-tight text-ink">
              Freeway
            </span>
          )}
        </Link>
      </div>

      <nav className="flex-1 space-y-0.5 overflow-y-auto px-3 py-2">
        {navItems.map((item) => {
          const isActive =
            pathname === item.href ||
            (item.href !== "/" && pathname.startsWith(item.href));

          return (
            <Link
              key={item.href}
              href={item.href}
              aria-current={isActive ? "page" : undefined}
              className={cn(
                "group relative flex h-9 items-center gap-3 rounded-control px-2.5 text-sm font-medium",
                "transition-colors duration-150 ease-swift",
                isActive
                  ? "bg-brand/10 text-brand"
                  : "text-muted hover:bg-inset hover:text-ink",
                isCollapsed && "justify-center px-0"
              )}
              title={isCollapsed ? item.label : undefined}
            >
              {isActive && (
                <span
                  className="absolute inset-y-1.5 left-0 w-0.5 rounded-full bg-brand"
                  aria-hidden
                />
              )}
              <item.icon className="h-4 w-4 shrink-0" aria-hidden />
              {!isCollapsed && <span>{item.label}</span>}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-line p-3">
        {!isCollapsed && user && (
          <div className="mb-2 flex items-center gap-2.5 rounded-control px-2.5 py-2">
            <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-brand/12 text-xs font-semibold text-brand">
              {(user.name || user.email).charAt(0).toUpperCase()}
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-medium text-ink">
                {user.name || user.email.split("@")[0]}
              </span>
              <span className="block truncate text-xs text-subtle">{user.email}</span>
            </span>
          </div>
        )}

        <button
          onClick={() => setIsCollapsed(!isCollapsed)}
          className={cn(
            "flex h-9 w-full items-center gap-3 rounded-control px-2.5 text-sm font-medium",
            "text-muted transition-colors hover:bg-inset hover:text-ink",
            isCollapsed && "justify-center px-0"
          )}
          aria-label={isCollapsed ? "Expand sidebar" : "Collapse sidebar"}
        >
          {isCollapsed ? (
            <PanelLeftOpen className="h-4 w-4" aria-hidden />
          ) : (
            <>
              <PanelLeftClose className="h-4 w-4" aria-hidden />
              <span>Collapse</span>
            </>
          )}
        </button>

        <button
          onClick={handleLogout}
          className={cn(
            "flex h-9 w-full items-center gap-3 rounded-control px-2.5 text-sm font-medium",
            "text-muted transition-colors hover:bg-danger/10 hover:text-danger",
            isCollapsed && "justify-center px-0"
          )}
          title={isCollapsed ? "Sign out" : undefined}
        >
          <LogOut className="h-4 w-4 shrink-0" aria-hidden />
          {!isCollapsed && <span>Sign out</span>}
        </button>
      </div>
    </aside>
  );
}
