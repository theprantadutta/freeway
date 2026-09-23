"use client";

import { useEffect, useState } from "react";
import {
  CheckCircle2,
  CircleAlert,
  Key,
  Link as LinkIcon,
  Monitor,
  Moon,
  Shield,
  Sun,
} from "lucide-react";
import { PageHead } from "@/components/shell/page-head";
import { Panel, PanelHead, Rule } from "@/components/ui/panel";
import { Tag } from "@/components/ui/tag";
import { Mark } from "@/components/brand/mark";
import { useAuthStore } from "@/lib/stores/auth-store";
import { useThemeStore } from "@/lib/stores/theme-store";
import { LANES, LANE_ORDER } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";
import { dateTime } from "@/lib/utils/format";

export default function SettingsPage() {
  const user = useAuthStore((s) => s.user);
  const { theme, setTheme } = useThemeStore();
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

  return (
    <div className="space-y-7">
      <PageHead title="Settings" lede="Your account, this console, and what the colours mean." />

      <section className="grid gap-4 lg:grid-cols-2">
        <Panel>
          <PanelHead title="Account" />
          <div className="flex items-start gap-4 p-4">
            <span className="grid h-11 w-11 shrink-0 place-items-center rounded bg-accent/15 text-base font-semibold text-accent">
              {(user?.name || user?.email || "?").charAt(0).toUpperCase()}
            </span>
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <p className="truncate text-sm font-semibold text-text">
                  {user?.name || user?.email?.split("@")[0]}
                </p>
                {user?.is_admin && (
                  <Tag tone="warn" dot>
                    <Shield className="h-2.5 w-2.5" aria-hidden />
                    admin
                  </Tag>
                )}
              </div>
              <p className="truncate text-xs text-text-3">{user?.email}</p>
              <dl className="mt-3 grid gap-3 sm:grid-cols-2">
                <div>
                  <dt className="text-2xs uppercase text-text-3">Member since</dt>
                  <dd className="mt-0.5 text-xs text-text-2">
                    {user?.created_at ? dateTime(user.created_at) : "—"}
                  </dd>
                </div>
                <div>
                  <dt className="text-2xs uppercase text-text-3">Last signed in</dt>
                  <dd className="mt-0.5 text-xs text-text-2">
                    {user?.last_login_at ? dateTime(user.last_login_at) : "—"}
                  </dd>
                </div>
              </dl>
            </div>
          </div>
        </Panel>

        <Panel>
          <PanelHead title="Gateway" action={<ApiStatus apiUrl={apiUrl} />} />
          <dl className="divide-y divide-hair">
            <Row icon={LinkIcon} label="API endpoint" value={apiUrl} mono />
            <Row icon={Key} label="Authentication" value="JWT bearer token" />
          </dl>
        </Panel>
      </section>

      <section>
        <Rule>Appearance</Rule>
        <Panel>
          <fieldset className="p-4">
            <legend className="sr-only">Theme</legend>
            <div className="grid grid-cols-3 gap-2">
              {(
                [
                  { value: "dark", label: "Dark", icon: Moon },
                  { value: "light", label: "Light", icon: Sun },
                  { value: "system", label: "System", icon: Monitor },
                ] as const
              ).map((o) => {
                const on = theme === o.value;
                return (
                  <button
                    key={o.value}
                    onClick={() => setTheme(o.value)}
                    aria-pressed={on}
                    className={cn(
                      "flex flex-col items-center gap-2 rounded border px-4 py-4 transition-colors duration-150 ease-out",
                      on
                        ? "border-accent bg-accent/[0.08] text-accent"
                        : "border-hair text-text-3 hover:border-hair-bright hover:text-text-2"
                    )}
                  >
                    <o.icon className="h-4 w-4" aria-hidden />
                    <span className="text-sm font-medium">{o.label}</span>
                  </button>
                );
              })}
            </div>
            <p className="mt-3 text-xs text-text-3">
              The console is designed dark first. Light is a separate set of values rather than an
              inversion of it.
            </p>
          </fieldset>
        </Panel>
      </section>

      <section>
        <Rule>Lane colours</Rule>
        <Panel>
          <p className="border-b border-hair px-4 py-3 text-sm text-text-2">
            A request type keeps its colour everywhere in the console. The paid tiers climb green to
            rose as cost rises, so relative spend is legible before any number is read.
          </p>
          <ul className="divide-y divide-hair">
            {LANE_ORDER.map((key) => {
              const lane = LANES[key];
              return (
                <li key={key} className={cn(lane.scope, "flex items-center gap-3 px-4 py-3")}>
                  <span className="h-6 w-1 shrink-0 rounded-full lane-bg" aria-hidden />
                  <span className="grid h-7 w-7 shrink-0 place-items-center rounded lane-soft lane-fg">
                    <lane.icon className="h-3.5 w-3.5" aria-hidden />
                  </span>
                  <span className="w-20 shrink-0 text-sm font-medium text-text">{lane.label}</span>
                  <code className="shrink-0 rounded bg-sunken px-1.5 py-0.5 font-mono text-2xs lane-fg">
                    {lane.model}
                  </code>
                  <span className="hidden min-w-0 flex-1 truncate text-xs text-text-3 sm:block">
                    {lane.blurb}
                  </span>
                </li>
              );
            })}
          </ul>
        </Panel>
      </section>

      <div className="flex items-center gap-2.5 text-xs text-text-3">
        <span className="h-5 w-5">
          <Mark />
        </span>
        Freeway console
        <span className="fig">v2.0</span>
      </div>
    </div>
  );
}

function Row({
  icon: Icon,
  label,
  value,
  mono,
}: {
  icon: typeof Key;
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="flex items-center gap-3 px-4 py-3">
      <Icon className="h-3.5 w-3.5 shrink-0 text-text-3" aria-hidden />
      <dt className="w-32 shrink-0 text-xs text-text-3">{label}</dt>
      <dd className={cn("min-w-0 flex-1 truncate text-xs text-text", mono && "font-mono")}>
        {value}
      </dd>
    </div>
  );
}

/** A live check, so "connection" is a fact on screen rather than a label. */
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

  if (state === "checking") return <Tag>checking…</Tag>;

  return state === "up" ? (
    <Tag tone="ok" dot>
      <CheckCircle2 className="h-2.5 w-2.5" aria-hidden />
      reachable
    </Tag>
  ) : (
    <Tag tone="bad" dot>
      <CircleAlert className="h-2.5 w-2.5" aria-hidden />
      unreachable
    </Tag>
  );
}
