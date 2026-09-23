"use client";

import { useEffect, useState } from "react";
import {
  Link as LinkIcon,
  Key,
  Sun,
  Moon,
  Monitor,
  LogOut,
  Shield,
  CheckCircle2,
  CircleAlert,
} from "lucide-react";
import { useAuthStore } from "@/lib/stores/auth-store";
import { useThemeStore } from "@/lib/stores/theme-store";
import { Header } from "@/components/layout/header";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { LaneMark } from "@/components/brand/lane-mark";
import { ACCENTS, type AccentKey } from "@/lib/theme/accents";
import { cn } from "@/lib/utils/cn";
import { formatDateTime } from "@/lib/utils/format";

export default function SettingsPage() {
  const user = useAuthStore((state) => state.user);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const { theme, setTheme } = useThemeStore();

  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

  const handleLogout = () => {
    clearAuth();
    window.location.href = "/login";
  };

  return (
    <div className="flex h-full flex-col">
      <Header title="Settings" subtitle="Your account and this panel" />

      <div className="flex-1 space-y-6 p-4 md:p-6">
        <Card>
          <CardHeader>
            <CardTitle>Account</CardTitle>
            <Button variant="ghost" size="sm" onClick={handleLogout}>
              <LogOut className="h-3.5 w-3.5" aria-hidden />
              Sign out
            </Button>
          </CardHeader>
          <CardContent>
            <div className="flex items-start gap-4">
              <span className="grid h-11 w-11 shrink-0 place-items-center rounded-panel bg-brand/12 text-base font-semibold text-brand">
                {(user?.name || user?.email || "?").charAt(0).toUpperCase()}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="truncate text-base font-semibold text-ink">
                    {user?.name || user?.email?.split("@")[0]}
                  </h3>
                  {user?.is_admin && (
                    <Badge variant="warning" dot>
                      <Shield className="h-3 w-3" aria-hidden />
                      Admin
                    </Badge>
                  )}
                </div>
                <p className="truncate text-sm text-muted">{user?.email}</p>
                <dl className="mt-3 grid gap-3 sm:grid-cols-2">
                  <div>
                    <dt className="text-xs text-subtle">Member since</dt>
                    <dd className="mt-0.5 text-sm text-ink">
                      {user?.created_at ? formatDateTime(user.created_at) : "—"}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs text-subtle">Last signed in</dt>
                    <dd className="mt-0.5 text-sm text-ink">
                      {user?.last_login_at
                        ? formatDateTime(user.last_login_at)
                        : "—"}
                    </dd>
                  </div>
                </dl>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Gateway connection</CardTitle>
            <ApiStatus apiUrl={apiUrl} />
          </CardHeader>
          <CardContent className="divide-y divide-line py-0">
            <SettingsRow
              icon={LinkIcon}
              accent={ACCENTS.low.scope}
              title="API endpoint"
              value={apiUrl}
              mono
            />
            <SettingsRow
              icon={Key}
              accent={ACCENTS.moderate.scope}
              title="Authentication"
              value="JWT bearer token"
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Appearance</CardTitle>
          </CardHeader>
          <CardContent>
            <fieldset>
              <legend className="text-sm text-muted">
                Theme. System follows your device setting.
              </legend>
              <div className="mt-3 grid grid-cols-3 gap-2.5">
                {(
                  [
                    { value: "light", label: "Light", icon: Sun },
                    { value: "system", label: "System", icon: Monitor },
                    { value: "dark", label: "Dark", icon: Moon },
                  ] as const
                ).map((option) => {
                  const isActive = theme === option.value;
                  return (
                    <button
                      key={option.value}
                      onClick={() => setTheme(option.value)}
                      aria-pressed={isActive}
                      className={cn(
                        "flex flex-col items-center gap-2 rounded-panel border px-4 py-4",
                        "transition-[border-color,background-color] duration-150 ease-swift",
                        isActive
                          ? "border-brand bg-brand/[0.07] text-brand"
                          : "border-line text-muted hover:border-line-strong hover:text-ink"
                      )}
                    >
                      <option.icon className="h-4 w-4" aria-hidden />
                      <span className="text-sm font-medium">{option.label}</span>
                    </button>
                  );
                })}
              </div>
            </fieldset>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Lane colours</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted">
              Each request type keeps the same colour across the panel. The paid
              tiers run from green to rose as cost rises.
            </p>
            <ul className="mt-4 grid gap-2 sm:grid-cols-2">
              {(
                ["free", "low", "moderate", "premium", "image"] as AccentKey[]
              ).map((key) => {
                const def = ACCENTS[key];
                const Icon = def.icon;
                return (
                  <li
                    key={key}
                    className={cn(
                      def.scope,
                      "flex items-center gap-3 rounded-control border border-line px-3.5 py-2.5"
                    )}
                  >
                    <span className="grid h-7 w-7 shrink-0 place-items-center rounded-[0.3125rem] accent-tint accent-text">
                      <Icon className="h-3.5 w-3.5" aria-hidden />
                    </span>
                    <span className="min-w-0">
                      <span className="block text-sm font-medium text-ink">
                        {def.label}
                      </span>
                      <code className="block truncate font-mono text-xs text-subtle">
                        {def.model}
                      </code>
                    </span>
                  </li>
                );
              })}
            </ul>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center gap-3">
            <span className="h-7 w-7 shrink-0">
              <LaneMark />
            </span>
            <p className="text-sm text-muted">
              Freeway control panel
              <span className="mx-1.5 text-subtle">·</span>
              <span className="tnum">v1.0.0</span>
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

/** Live check so "connection" is a fact on screen rather than a label. */
function ApiStatus({ apiUrl }: { apiUrl: string }) {
  const [state, setState] = useState<"checking" | "up" | "down">("checking");

  useEffect(() => {
    let cancelled = false;
    fetch(`${apiUrl}/health`, { cache: "no-store" })
      .then((r) => {
        if (!cancelled) setState(r.ok ? "up" : "down");
      })
      .catch(() => {
        if (!cancelled) setState("down");
      });
    return () => {
      cancelled = true;
    };
  }, [apiUrl]);

  if (state === "checking") {
    return <Badge variant="default">Checking…</Badge>;
  }

  return state === "up" ? (
    <Badge variant="success" dot>
      <CheckCircle2 className="h-3 w-3" aria-hidden />
      Reachable
    </Badge>
  ) : (
    <Badge variant="error" dot>
      <CircleAlert className="h-3 w-3" aria-hidden />
      Unreachable
    </Badge>
  );
}

function SettingsRow({
  icon: Icon,
  accent,
  title,
  value,
  mono,
}: {
  icon: typeof Key;
  accent: string;
  title: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className={cn(accent, "flex items-center gap-3.5 py-4")}>
      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-control accent-tint accent-text">
        <Icon className="h-4 w-4" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-ink">{title}</p>
        <p className={cn("mt-0.5 truncate text-sm text-muted", mono && "font-mono")}>
          {value}
        </p>
      </div>
    </div>
  );
}
