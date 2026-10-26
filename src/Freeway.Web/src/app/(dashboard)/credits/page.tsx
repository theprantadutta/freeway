"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowRight, CalendarClock, Coins, KeyRound, Mail, RefreshCw } from "lucide-react";
import { PageHead } from "@/components/shell/page-head";
import { Panel, PanelHead, PanelBody, Rule } from "@/components/ui/panel";
import { Tag } from "@/components/ui/tag";
import { Pulse } from "@/components/ui/pulse";
import { Failed, Empty } from "@/components/ui/state";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { SeriesChart } from "@/components/chart/series-chart";
import { creditsApi } from "@/lib/api/credits";
import { laneOf } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";
import { ago, compact, count, dateTime, fullDate, modelShortName, money, ms, pct } from "@/lib/utils/format";
import type { CreditOverview, CreditState } from "@/lib/types";

const STATE: Record<CreditState, { label: string; tone: "ok" | "warn" | "bad" | "neutral" }> = {
  active: { label: "in use", tone: "ok" },
  exhausted: { label: "used up", tone: "warn" },
  blocked: { label: "paused", tone: "bad" },
  no_grant: { label: "no credit this cycle", tone: "warn" },
  unchecked: { label: "counting…", tone: "neutral" },
  off: { label: "off", tone: "neutral" },
};

export default function CreditsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const q = useQuery({
    queryKey: ["credits"],
    queryFn: creditsApi.getOverview,
    refetchInterval: 60_000,
  });

  const check = useMutation({
    mutationFn: creditsApi.check,
    onSuccess: (r) => {
      toast(r.can_serve ? "Key works; the credit is in use" : `Checked: ${r.summary}`, r.can_serve ? "success" : "info");
      queryClient.invalidateQueries({ queryKey: ["credits"] });
    },
    onError: () => toast("The check failed; the gateway did not answer", "error"),
  });

  const c = q.data;

  return (
    <div className="space-y-7">
      <PageHead
        title="Credits"
        lede="The Claude plan's monthly API credit. Every paid lane spends it first, then the Claude Code bridge, and only then OpenRouter."
        actions={
          <Button size="sm" onClick={() => check.mutate()} busy={check.isPending} disabled={!c?.configured}>
            <RefreshCw className="h-3.5 w-3.5" aria-hidden />
            Check now
          </Button>
        }
      />

      {q.isError ? (
        <Failed title="Could not load the credit" onRetry={() => q.refetch()} />
      ) : q.isLoading || !c ? (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,24rem)_minmax(0,1fr)]">
          <Pulse className="h-72" />
          <Pulse className="h-72" />
        </div>
      ) : !c.configured ? (
        <Panel>
          <Empty
            className="lane-premium"
            icon={Coins}
            title="The Claude API credit is off"
            body="Set ANTHROPIC_API_KEY in the server's .env (and leave ANTHROPIC_CREDIT_ENABLED unset or true), then restart the API. Until then every lane works exactly as before."
          />
        </Panel>
      ) : (
        <>
          <section className="enter grid gap-4 lg:grid-cols-[minmax(0,24rem)_minmax(0,1fr)]">
            <Balance c={c} />
            <Panel className="flex min-h-[20rem] flex-col">
              <SeriesChart
                className="flex-1 pb-4"
                points={c.series.map((d) => ({
                  date: d.date,
                  requests: d.requests,
                  failures: 0,
                  cost: d.credit_usd,
                  tokens: d.tokens,
                }))}
              />
            </Panel>
          </section>

          <section className="grid gap-4 lg:grid-cols-2">
            <Chain c={c} />
            <KeyPanel c={c} />
          </section>

          <section>
            <Rule>What spent it · this cycle</Rule>
            <div className="grid gap-4 lg:grid-cols-2">
              <Breakdown title="By project" rows={c.by_project} total={c.used_usd} />
              <Breakdown title="By model" rows={c.by_model} total={c.used_usd} models />
            </div>
          </section>

          <section>
            <Rule>Recent requests</Rule>
            <Recent c={c} />
          </section>

          <section>
            <Rule>Cycles</Rule>
            <div className="grid gap-4 lg:grid-cols-2">
              {c.grant && <GrantEditor c={c} />}
              <History c={c} />
            </div>
          </section>
        </>
      )}
    </div>
  );
}

/** The figure this page exists for: how much of the credit is left, and where it is heading. */
function Balance({ c }: { c: CreditOverview }) {
  const state = STATE[c.state];
  const grant = c.grant;
  const used = grant ? Math.min(100, (c.used_usd / Math.max(grant.amount_usd, 0.0001)) * 100) : 0;

  return (
    <Panel className="lane-premium lane-halo flex flex-col justify-between p-5">
      <div>
        <div className="flex items-center justify-between gap-3">
          <p className="text-2xs font-medium uppercase text-text-3">Left this cycle</p>
          <Tag tone={state.tone} dot>
            {state.label}
          </Tag>
        </div>
        <p className="fig-hero mt-3 text-5xl text-text">{money(c.remaining_usd)}</p>
        <p className="mt-1 text-sm text-text-2">
          {money(c.used_usd)} of {money(grant?.amount_usd ?? 0)} used
          {grant && <> · resets {fullDate(grant.expires_at)}</>}
        </p>

        {/* The bar runs to the grant; the tick is where Freeway stops using it. */}
        <div className="relative mt-4 h-2 w-full overflow-hidden rounded-full bg-sunken" aria-hidden>
          <div
            className={cn(
              "h-full rounded-full transition-[width] duration-500 ease-out",
              c.state === "exhausted" ? "bg-warn" : "lane-bg"
            )}
            style={{ width: `${Math.max(used, used > 0 ? 1 : 0)}%` }}
          />
          <span className="absolute inset-y-0 w-px bg-text-2" style={{ left: `${c.stop_at_percent}%` }} />
        </div>
        <p className="mt-1.5 text-2xs text-text-3">
          {pct(c.used_percent, 2)} used · stops at {pct(c.stop_at_percent)} ({money(c.usable_usd)})
        </p>

        {(c.state === "blocked" || c.state === "no_grant") && (
          <p className="mt-3 rounded border border-bad/30 bg-bad/[0.07] px-3 py-2 text-xs text-text-2">
            {c.summary}
          </p>
        )}
      </div>

      <dl className="mt-6 grid grid-cols-2 gap-x-4 gap-y-3 border-t border-hair pt-4">
        <Figure label="Days left" value={grant ? c.days_left.toFixed(1) : "—"} />
        <Figure label="Per day" value={money(c.burn_per_day_usd)} />
        <Figure label="Projected" value={money(c.projected_usd)} />
        <Figure label="Will expire unused" value={money(c.projected_unused_usd)} />
        <Figure label="Requests" value={count(c.requests)} />
        <Figure label="Saved vs paid" value={money(c.avoided_usd)} />
      </dl>
    </Panel>
  );
}

/** The order a paid request tries its backends in, with where each stands. */
function Chain({ c }: { c: CreditOverview }) {
  const steps = [
    {
      name: "Claude API credit",
      detail: c.lanes.map((l) => `${l.tier} → ${l.model}`).join(" · ") || "no lanes",
      tag: <Tag tone={STATE[c.state].tone}>{STATE[c.state].label}</Tag>,
    },
    {
      name: "Claude Code bridge",
      detail: "The Max subscription on the server. Free, within the plan's limits.",
      tag: <Tag>next</Tag>,
    },
    {
      name: "OpenRouter",
      detail: "Billed. Only when both of the above could not answer.",
      tag: <Tag tone="warn">last resort</Tag>,
    },
  ];

  return (
    <Panel>
      <PanelHead
        title="Where paid requests go"
        meta={c.refreshed_at ? `Counted ${ago(c.refreshed_at)}` : undefined}
      />
      <ol className="divide-y divide-hair">
        {steps.map((s, i) => (
          <li key={s.name} className="flex items-start gap-3 px-4 py-3">
            <span className="fig grid h-6 w-6 shrink-0 place-items-center rounded bg-sunken text-2xs text-text-2">
              {i + 1}
            </span>
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm font-medium text-text">{s.name}</span>
                {s.tag}
              </div>
              <p className="mt-0.5 text-xs text-text-3">{s.detail}</p>
            </div>
            {i < steps.length - 1 && <ArrowRight className="mt-1 h-3.5 w-3.5 shrink-0 text-text-3" aria-hidden />}
          </li>
        ))}
      </ol>
      {c.last_error && (
        <p className="border-t border-hair px-4 py-3 text-xs text-text-3">
          Last problem{c.last_error_at ? ` (${ago(c.last_error_at)})` : ""}:{" "}
          <span className="text-text-2">{c.last_error.split("\n")[0]}</span>
        </p>
      )}
    </Panel>
  );
}

/** The API key: Freeway cannot read its expiry, so it shows its estimate and lets you correct it. */
function KeyPanel({ c }: { c: CreditOverview }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const key = c.key;
  const [expires, setExpires] = useState("");

  useEffect(() => {
    setExpires(key?.expires_at ? toLocalInput(key.expires_at) : "");
  }, [key?.expires_at]);

  const save = useMutation({
    mutationFn: () => creditsApi.setKeyExpiry(expires ? new Date(expires).toISOString() : null),
    onSuccess: () => {
      toast("Key expiry saved; reminders re-armed", "success");
      queryClient.invalidateQueries({ queryKey: ["credits"] });
    },
    onError: () => toast("Could not save the expiry", "error"),
  });

  const test = useMutation({
    mutationFn: creditsApi.sendTestReminder,
    onSuccess: (r) => toast(r.sent ? `Test reminder sent to ${r.recipient}` : r.detail, r.sent ? "success" : "error"),
    onError: () => toast("Could not send the test reminder", "error"),
  });

  const days = key?.days_left ?? null;
  const tone = days == null ? "neutral" : days <= 0 ? "bad" : days <= 3 ? "warn" : "ok";

  return (
    <Panel>
      <PanelHead
        title="API key"
        meta="Emailed 3 days before it expires, and at once if Anthropic rejects it"
        action={
          key && (
            <Tag tone={tone} dot>
              {days == null ? "no expiry" : days <= 0 ? "expired" : `${Math.ceil(days)} days left`}
            </Tag>
          )
        }
      />
      {!key ? (
        <PanelBody>
          <p className="text-sm text-text-2">Not seen yet. It is recorded on the first count after startup.</p>
        </PanelBody>
      ) : (
        <>
          <dl className="divide-y divide-hair">
            <Row icon={KeyRound} label="Key" value={key.hint} mono />
            <Row icon={CalendarClock} label="First seen" value={dateTime(key.first_seen_at)} />
            <Row
              icon={CalendarClock}
              label="Expires"
              value={
                key.expires_at
                  ? `${dateTime(key.expires_at)} · ${key.expiry_source === "manual" ? "set by you" : "estimated"}`
                  : "never"
              }
            />
            {key.warned_at && <Row icon={Mail} label="Reminder sent" value={dateTime(key.warned_at)} />}
            {key.last_rejected_at && (
              <Row icon={KeyRound} label="Last rejected" value={`${dateTime(key.last_rejected_at)}`} tone="bad" />
            )}
          </dl>
          <form
            className="flex flex-wrap items-end gap-2 border-t border-hair p-4"
            onSubmit={(e) => {
              e.preventDefault();
              save.mutate();
            }}
          >
            <div className="min-w-[14rem] flex-1">
              <Field
                label="Expires on (from the Console)"
                type="datetime-local"
                value={expires}
                onChange={(e) => setExpires(e.target.value)}
                hint="Leave empty for a key that never expires."
              />
            </div>
            <Button type="submit" size="md" busy={save.isPending}>
              Save
            </Button>
            <Button type="button" size="md" variant="ghost" busy={test.isPending} onClick={() => test.mutate()}>
              <Mail className="h-3.5 w-3.5" aria-hidden />
              Test email
            </Button>
          </form>
        </>
      )}
    </Panel>
  );
}

function Breakdown({
  title,
  rows,
  total,
  models,
}: {
  title: string;
  rows: CreditOverview["by_project"];
  total: number;
  models?: boolean;
}) {
  return (
    <Panel>
      <PanelHead title={title} meta="Share of the credit spent" />
      {rows.length === 0 ? (
        <p className="px-4 py-6 text-sm text-text-3">Nothing yet this cycle.</p>
      ) : (
        <ul className="divide-y divide-hair">
          {rows.map((r) => {
            const lane = laneOf("paid", r.tier);
            const share = total > 0 ? (r.credit_usd / total) * 100 : 0;
            return (
              <li key={r.key + (r.tier ?? "")} className={cn(models && lane.scope, "px-4 py-2.5")}>
                <div className="flex items-baseline justify-between gap-3">
                  <span className={cn("truncate text-xs text-text", models && "font-mono")}>
                    {models ? modelShortName(r.name) : r.name}
                  </span>
                  <span className="fig shrink-0 text-xs text-text-2">{money(r.credit_usd)}</span>
                </div>
                <div className="mt-1.5 h-1 w-full overflow-hidden rounded-full bg-sunken">
                  <div
                    className={cn("h-full rounded-full", models ? "lane-bg" : "bg-accent")}
                    style={{ width: `${Math.max(share, 2)}%` }}
                  />
                </div>
                <div className="mt-1 flex items-center justify-between text-2xs text-text-3">
                  <span>{models && r.tier ? lane.label : `${count(r.requests)} requests`}</span>
                  <span className="fig">
                    {models ? `${count(r.requests)} req · ` : ""}
                    {compact(r.tokens)} tokens
                  </span>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </Panel>
  );
}

function Recent({ c }: { c: CreditOverview }) {
  if (c.recent.length === 0) {
    return (
      <Panel>
        <p className="px-4 py-6 text-sm text-text-3">No request has used the credit this cycle yet.</p>
      </Panel>
    );
  }

  return (
    <Panel className="overflow-x-auto">
      <table className="w-full min-w-[40rem] text-left text-xs">
        <thead className="border-b border-hair text-2xs uppercase text-text-3">
          <tr>
            <th className="px-4 py-2 font-medium">When</th>
            <th className="px-4 py-2 font-medium">Project</th>
            <th className="px-4 py-2 font-medium">Model</th>
            <th className="px-4 py-2 text-right font-medium">Tokens</th>
            <th className="px-4 py-2 text-right font-medium">Time</th>
            <th className="px-4 py-2 text-right font-medium">Credit</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-hair">
          {c.recent.map((r) => {
            const lane = laneOf("paid", r.model_tier);
            return (
              <tr key={r.id} className={lane.scope}>
                <td className="whitespace-nowrap px-4 py-2 text-text-3">{ago(r.created_at)}</td>
                <td className="max-w-[10rem] truncate px-4 py-2 text-text">{r.project_name}</td>
                <td className="whitespace-nowrap px-4 py-2">
                  <span className="font-mono lane-fg">{modelShortName(r.model_id)}</span>
                  {r.streamed && <span className="ml-1.5 text-2xs text-text-3">stream</span>}
                </td>
                <td className="fig whitespace-nowrap px-4 py-2 text-right text-text-2">
                  {compact(r.input_tokens)} → {compact(r.output_tokens)}
                </td>
                <td className="fig whitespace-nowrap px-4 py-2 text-right text-text-2">{ms(r.response_time_ms)}</td>
                <td className="fig whitespace-nowrap px-4 py-2 text-right text-text">{money(r.credit_usd)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </Panel>
  );
}

/** Correct the current cycle to match the Console's Billing page. */
function GrantEditor({ c }: { c: CreditOverview }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const grant = c.grant!;
  const [amount, setAmount] = useState(String(grant.amount_usd));
  const [starts, setStarts] = useState(toLocalInput(grant.starts_at));
  const [expires, setExpires] = useState(toLocalInput(grant.expires_at));

  useEffect(() => {
    setAmount(String(grant.amount_usd));
    setStarts(toLocalInput(grant.starts_at));
    setExpires(toLocalInput(grant.expires_at));
  }, [grant.amount_usd, grant.starts_at, grant.expires_at]);

  const save = useMutation({
    mutationFn: () =>
      creditsApi.updateGrant(grant.id, {
        amount_usd: Number(amount),
        starts_at: new Date(starts).toISOString(),
        expires_at: new Date(expires).toISOString(),
      }),
    onSuccess: () => {
      toast("Cycle updated", "success");
      queryClient.invalidateQueries({ queryKey: ["credits"] });
    },
    onError: (e: Error & { detail?: string }) => toast(e.detail || "Could not update the cycle", "error"),
  });

  return (
    <Panel>
      <PanelHead
        title="This cycle"
        meta={grant.source === "manual" ? "Edited by you" : "Opened automatically each month"}
      />
      <form
        className="grid gap-3 p-4 sm:grid-cols-3"
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate();
        }}
      >
        <Field label="Amount (USD)" type="number" min={0} step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <Field label="Starts" type="datetime-local" value={starts} onChange={(e) => setStarts(e.target.value)} />
        <Field label="Expires" type="datetime-local" value={expires} onChange={(e) => setExpires(e.target.value)} />
        <div className="flex items-center justify-between gap-3 sm:col-span-3">
          <p className="text-xs text-text-3">
            Match the Console&apos;s Billing page. The next cycle opens where this one ends.
          </p>
          <Button type="submit" busy={save.isPending}>
            Save
          </Button>
        </div>
      </form>
    </Panel>
  );
}

function History({ c }: { c: CreditOverview }) {
  return (
    <Panel>
      <PanelHead title="Past and current cycles" />
      {c.history.length === 0 ? (
        <p className="px-4 py-6 text-sm text-text-3">No cycles recorded yet.</p>
      ) : (
        <ul className="divide-y divide-hair">
          {c.history.map((g) => {
            const share = g.amount_usd > 0 ? (g.used_usd / g.amount_usd) * 100 : 0;
            const current = g.id === c.grant?.id;
            return (
              <li key={g.id} className="px-4 py-2.5">
                <div className="flex items-baseline justify-between gap-3">
                  <span className="text-xs text-text">
                    {fullDate(g.starts_at)} – {fullDate(g.expires_at)}
                    {current && (
                      <Tag tone="accent" className="ml-2">
                        current
                      </Tag>
                    )}
                  </span>
                  <span className="fig shrink-0 text-xs text-text-2">
                    {money(g.used_usd)} / {money(g.amount_usd)}
                  </span>
                </div>
                <div className="mt-1.5 h-1 w-full overflow-hidden rounded-full bg-sunken">
                  <div className="h-full rounded-full bg-accent" style={{ width: `${Math.min(Math.max(share, 1), 100)}%` }} />
                </div>
                <p className="mt-1 text-2xs text-text-3">
                  {count(g.requests)} requests · {pct(share, 1)} used
                </p>
              </li>
            );
          })}
        </ul>
      )}
    </Panel>
  );
}

function Figure({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-2xs uppercase text-text-3">{label}</dt>
      <dd className="fig mt-0.5 text-sm text-text">{value}</dd>
    </div>
  );
}

function Row({
  icon: Icon,
  label,
  value,
  mono,
  tone,
}: {
  icon: typeof KeyRound;
  label: string;
  value: string;
  mono?: boolean;
  tone?: "bad";
}) {
  return (
    <div className="flex items-center gap-3 px-4 py-2.5">
      <Icon className="h-3.5 w-3.5 shrink-0 text-text-3" aria-hidden />
      <dt className="w-28 shrink-0 text-xs text-text-3">{label}</dt>
      <dd className={cn("min-w-0 flex-1 truncate text-xs", tone === "bad" ? "text-bad" : "text-text", mono && "font-mono")}>
        {value}
      </dd>
    </div>
  );
}

/** An ISO timestamp as the value a datetime-local input expects, in the viewer's time zone. */
function toLocalInput(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
