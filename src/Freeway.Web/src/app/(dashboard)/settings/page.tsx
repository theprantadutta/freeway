"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Coins, Globe, Monitor, Moon, RefreshCw, Shield, SquareTerminal, Sun } from "lucide-react";
import { PageHead } from "@/components/shell/page-head";
import { Panel, PanelHead, Rule } from "@/components/ui/panel";
import { Button } from "@/components/ui/button";
import { Tag } from "@/components/ui/tag";
import { useToast } from "@/components/ui/toast";
import { Meter } from "@/components/charts/primitives";
import { Mark } from "@/components/brand/mark";
import { analyticsApi } from "@/lib/api/analytics";
import { creditsApi } from "@/lib/api/credits";
import { useAuthStore } from "@/lib/stores/auth-store";
import { useThemeStore } from "@/lib/stores/theme-store";
import { LANES, LANE_ORDER } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";
import { ago, cents, dateTime, ms } from "@/lib/utils/format";

type Theme = "dark" | "light" | "system";

export default function SettingsPage() {
  const user = useAuthStore((s) => s.user);
  const name = user?.name || user?.email?.split("@")[0] || "Signed in";

  return (
    <div className="space-y-10">
      <PageHead title="Settings" lede="Your account, how the console looks, and the health of everything a request depends on." />

      <section className="reveal grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]" style={{ ["--d" as string]: "80ms" }}>
        <Panel className="tint-violet glow p-7">
          <div className="flex items-center gap-5">
            <span className="grad grid h-20 w-20 shrink-0 place-items-center rounded-2xl text-3xl font-extrabold text-white shadow-[0_12px_30px_-12px_rgb(var(--grad-b)/0.8)]">
              {name.charAt(0).toUpperCase()}
            </span>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="truncate text-2xl font-extrabold text-text">{name}</h2>
                {user?.is_admin && (
                  <Tag tone="accent" dot>
                    <Shield className="h-3 w-3" aria-hidden />
                    Administrator
                  </Tag>
                )}
              </div>
              <p className="mt-1 truncate text-base text-text-2">{user?.email}</p>
            </div>
          </div>
          <dl className="mt-8 grid gap-5 sm:grid-cols-2">
            <div className="rounded-xl bg-sunken/70 p-4 ring-1 ring-hair">
              <dt className="text-sm text-text-3">Member since</dt>
              <dd className="mt-1 text-base font-semibold text-text">{user?.created_at ? dateTime(user.created_at) : "Unknown"}</dd>
            </div>
            <div className="rounded-xl bg-sunken/70 p-4 ring-1 ring-hair">
              <dt className="text-sm text-text-3">Last signed in</dt>
              <dd className="mt-1 text-base font-semibold text-text">{user?.last_login_at ? dateTime(user.last_login_at) : "Now"}</dd>
            </div>
          </dl>
        </Panel>

        <Appearance />
      </section>

      <section className="reveal" style={{ ["--d" as string]: "160ms" }}>
        <Rule description="Everything a request passes through, as it stands right now.">Gateway health</Rule>
        <div className="grid gap-6 lg:grid-cols-3">
          <ApiHealth />
          <BridgeHealth />
          <CreditHealth />
        </div>
      </section>

      <section className="reveal" style={{ ["--d" as string]: "240ms" }}>
        <Rule description="What a caller sends as the model, and what each lane is for. A lane keeps its colour everywhere in the console.">
          Lanes
        </Rule>
        <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-5">
          {LANE_ORDER.map((key) => {
            const lane = LANES[key];
            return (
              <Panel key={key} className={cn(lane.scope, "relative p-6")}>
                <span className="absolute inset-x-0 top-0 h-1 lane-bg" aria-hidden />
                <span className="grid h-11 w-11 place-items-center rounded-xl lane-soft">
                  <lane.icon className="h-5 w-5 lane-fg" aria-hidden />
                </span>
                <p className="mt-4 text-xl font-bold text-text">{lane.label}</p>
                <code className="mt-1 block font-mono text-sm text-text-2">&quot;model&quot;: &quot;{lane.model}&quot;</code>
                <p className="mt-3 text-base text-text-3">{lane.blurb}</p>
              </Panel>
            );
          })}
        </div>
      </section>

      <div className="flex items-center gap-3 text-base text-text-3">
        <span className="h-7 w-7">
          <Mark />
        </span>
        Freeway console, version 3
      </div>
    </div>
  );
}

/** Theme choices drawn as tiny previews of the console, not as words. */
function Appearance() {
  const { theme, setTheme } = useThemeStore();
  const options: { value: Theme; label: string; icon: typeof Moon; note: string }[] = [
    { value: "dark", label: "Dusk", icon: Moon, note: "The night sky" },
    { value: "light", label: "Daylight", icon: Sun, note: "Paper and pastel light" },
    { value: "system", label: "Match system", icon: Monitor, note: "Follows your device" },
  ];

  return (
    <Panel className="p-7">
      <h2 className="text-xl font-bold text-text">Appearance</h2>
      <p className="mt-1 text-base text-text-3">Choose how the console looks. Each theme has its own colours, not an inversion.</p>
      <div role="radiogroup" aria-label="Theme" className="mt-6 grid gap-4 sm:grid-cols-3">
        {options.map((o) => {
          const on = theme === o.value;
          return (
            <button
              key={o.value}
              role="radio"
              aria-checked={on}
              onClick={() => setTheme(o.value)}
              className={cn(
                "group rounded-2xl p-2 text-left transition-[box-shadow,transform] duration-300 ease-out hover:-translate-y-0.5",
                on ? "ring-2 ring-[rgb(var(--accent))]" : "ring-1 ring-hair hover:ring-hair-bright"
              )}
            >
              <Preview variant={o.value} />
              <span className="mt-3 flex items-center gap-2 px-1.5">
                <o.icon className="h-4 w-4 text-text-2" aria-hidden />
                <span className="text-base font-bold text-text">{o.label}</span>
                {on && <Check className="ml-auto h-4 w-4 text-accent" aria-hidden />}
              </span>
              <span className="block px-1.5 pb-1 text-sm text-text-3">{o.note}</span>
            </button>
          );
        })}
      </div>
    </Panel>
  );
}

function Preview({ variant }: { variant: Theme }) {
  const dark = (
    <div className="h-full w-full" style={{ background: "radial-gradient(80% 60% at 0% 0%, rgb(217 70 239 / .5), transparent 60%), radial-gradient(60% 60% at 100% 0%, rgb(34 211 238 / .35), transparent 60%), #100b26" }}>
      <MiniBoard surface="rgb(27 20 60 / .85)" line="rgb(80 66 140)" />
    </div>
  );
  const light = (
    <div className="h-full w-full" style={{ background: "radial-gradient(80% 60% at 0% 0%, rgb(232 121 249 / .35), transparent 60%), radial-gradient(60% 60% at 100% 0%, rgb(103 232 249 / .35), transparent 60%), #faf7ff" }}>
      <MiniBoard surface="rgb(255 255 255 / .9)" line="rgb(214 205 238)" />
    </div>
  );
  return (
    <div className="relative h-24 overflow-hidden rounded-xl ring-1 ring-hair">
      {variant === "dark" ? dark : variant === "light" ? light : (
        <div className="grid h-full grid-cols-2">
          <div className="overflow-hidden">{dark}</div>
          <div className="overflow-hidden">{light}</div>
        </div>
      )}
    </div>
  );
}

function MiniBoard({ surface, line }: { surface: string; line: string }) {
  return (
    <div className="flex h-full gap-1.5 p-2.5">
      <div className="w-5 rounded-md" style={{ background: surface }} />
      <div className="flex flex-1 flex-col gap-1.5">
        <div className="h-2.5 w-3/4 rounded grad" />
        <div className="flex flex-1 gap-1.5">
          {["rgb(var(--free))", "rgb(var(--low))", "rgb(var(--premium))"].map((c) => (
            <div key={c} className="flex flex-1 items-end rounded-md p-1" style={{ background: surface, boxShadow: `inset 0 0 0 1px ${line}` }}>
              <div className="h-1/2 w-full rounded-sm" style={{ background: c }} />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function HealthCard({
  icon: Icon,
  tint,
  title,
  state,
  tone,
  children,
  action,
}: {
  icon: typeof Globe;
  tint: string;
  title: string;
  state: string;
  tone: "ok" | "bad" | "neutral" | "warn";
  children: React.ReactNode;
  action?: React.ReactNode;
}) {
  return (
    <Panel className={cn(tint, "glow flex flex-col p-6")}>
      <div className="flex items-start justify-between gap-3">
        <span className="grid h-11 w-11 place-items-center rounded-xl bg-sunken/80 ring-1 ring-hair">
          <Icon className="h-5 w-5 tint-fg" aria-hidden />
        </span>
        <Tag tone={tone} dot>{state}</Tag>
      </div>
      <h3 className="mt-4 text-xl font-bold text-text">{title}</h3>
      <div className="mt-3 flex-1 space-y-2 text-base text-text-2">{children}</div>
      {action && <div className="mt-5">{action}</div>}
    </Panel>
  );
}

function ApiHealth() {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";
  const [state, setState] = useState<{ ok: boolean | null; ms?: number }>({ ok: null });

  useEffect(() => {
    let cancelled = false;
    const start = performance.now();
    fetch(`${apiUrl}/health`, { cache: "no-store" })
      .then((r) => !cancelled && setState({ ok: r.ok, ms: performance.now() - start }))
      .catch(() => !cancelled && setState({ ok: false }));
    return () => {
      cancelled = true;
    };
  }, [apiUrl]);

  return (
    <HealthCard
      icon={Globe}
      tint="tint-sky"
      title="Gateway API"
      state={state.ok == null ? "Checking" : state.ok ? "Reachable" : "Unreachable"}
      tone={state.ok == null ? "neutral" : state.ok ? "ok" : "bad"}
    >
      <p className="break-all font-mono text-sm text-text">{apiUrl}</p>
      <p>{state.ms != null ? `Answered in ${ms(state.ms)}` : "Waiting for the health check"}</p>
      <p className="text-text-3">Signed in with a JWT that expires on its own.</p>
    </HealthCard>
  );
}

function BridgeHealth() {
  const qc = useQueryClient();
  const { toast } = useToast();
  const bridge = useQuery({ queryKey: ["local-claude"], queryFn: analyticsApi.getLocalClaude, refetchInterval: 60_000 });
  const check = useMutation({
    mutationFn: analyticsApi.checkLocalClaude,
    onSuccess: (r) => {
      toast(r.can_serve ? "Claude Code is ready" : `Claude Code: ${r.summary}`, r.can_serve ? "success" : "warning");
      qc.invalidateQueries({ queryKey: ["local-claude"] });
    },
    onError: () => toast("The check did not complete", "error"),
  });
  const b = bridge.data;
  const windows = b?.plan?.windows ?? {};

  return (
    <HealthCard
      icon={SquareTerminal}
      tint="tint-violet"
      title="Claude Code bridge"
      state={!b ? "Checking" : !b.configured ? "Off" : b.can_serve ? "Ready" : b.state === "ratelimited" ? "Rate limited" : "Unavailable"}
      tone={!b ? "neutral" : !b.configured ? "neutral" : b.can_serve ? "ok" : "bad"}
      action={
        b?.configured ? (
          <Button size="sm" onClick={() => check.mutate()} busy={check.isPending}>
            <RefreshCw className="h-4 w-4" aria-hidden />
            Check now
          </Button>
        ) : undefined
      }
    >
      {!b ? (
        <p>Reading the last check…</p>
      ) : !b.configured ? (
        <p>Turned off on this gateway. Set LOCAL_CLAUDE_ENABLED=true to serve paid lanes from the Max subscription.</p>
      ) : (
        <>
          <p>{b.summary}</p>
          {b.checked_at && <p className="text-text-3">Checked {ago(b.checked_at)}</p>}
          {Object.entries(windows).map(([k, w]) => (
            <div key={k} className="pt-1">
              <p className="mb-1.5 flex justify-between text-sm text-text-3">
                <span>Plan, {k === "five_hour" ? "5 hours" : k === "seven_day" ? "7 days" : k.replace(/_/g, " ")}</span>
                <span className="tnum font-semibold text-text-2">{Math.round(w.utilization_percent)}%</span>
              </p>
              <Meter value={w.utilization_percent} label={`Claude plan, ${k}`} />
            </div>
          ))}
        </>
      )}
    </HealthCard>
  );
}

function CreditHealth() {
  const credit = useQuery({ queryKey: ["credits"], queryFn: creditsApi.getOverview, refetchInterval: 60_000 });
  const c = credit.data;

  return (
    <HealthCard
      icon={Coins}
      tint="tint-pink"
      title="Claude API credit"
      state={!c ? "Checking" : !c.configured ? "Off" : c.can_serve ? "In use" : "Paused"}
      tone={!c ? "neutral" : !c.configured ? "neutral" : c.can_serve ? "ok" : "warn"}
      action={
        <Link href="/credits">
          <Button size="sm">Open credits</Button>
        </Link>
      }
    >
      {!c ? (
        <p>Counting…</p>
      ) : !c.configured ? (
        <p>No ANTHROPIC_API_KEY on this gateway, so paid lanes go straight to the bridge and OpenRouter.</p>
      ) : (
        <>
          <p>
            <span className="num text-2xl font-extrabold text-text">{cents(c.remaining_usd)}</span>{" "}
            <span className="text-text-3">left of {cents(c.grant?.amount_usd ?? 0)}</span>
          </p>
          <Meter value={c.used_percent} marker={c.stop_at_percent} label="Claude credit used" />
          <p className="text-text-3">{c.summary}</p>
          {c.key?.days_left != null && <p className="text-text-3">API key expires in {Math.ceil(c.key.days_left)} days</p>}
        </>
      )}
    </HealthCard>
  );
}
