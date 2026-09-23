"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Check,
  ChevronRight,
  Copy,
  FolderKanban,
  MoreVertical,
  Pencil,
  Plus,
  RefreshCw,
  Trash2,
  TriangleAlert,
} from "lucide-react";
import { PageHead } from "@/components/shell/page-head";
import { Panel } from "@/components/ui/panel";
import { Menu, MenuItem } from "@/components/ui/menu";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import { Tag } from "@/components/ui/tag";
import { Sheet, SheetActions } from "@/components/ui/sheet";
import { Pulse } from "@/components/ui/pulse";
import { Empty, Failed } from "@/components/ui/state";
import { useToast } from "@/components/ui/toast";
import { projectsApi } from "@/lib/api/projects";
import { analyticsApi } from "@/lib/api/analytics";
import { cn } from "@/lib/utils/cn";
import { compact, fullDate, money, pct, startOfMonthIso } from "@/lib/utils/format";
import { LANES, laneOf, type Lane } from "@/lib/theme/lanes";
import type { ModelUsageStats, Project } from "@/lib/types";

export default function ProjectsPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Project | null>(null);
  const [deleting, setDeleting] = useState<Project | null>(null);
  const [rotating, setRotating] = useState<Project | null>(null);
  const [freshKey, setFreshKey] = useState<string | null>(null);

  const list = useQuery({ queryKey: ["projects"], queryFn: () => projectsApi.getProjects() });

  const create = useMutation({
    mutationFn: projectsApi.createProject,
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ["projects"] });
      setCreating(false);
      if (r.api_key) setFreshKey(r.api_key);
      toast("Project created", "success");
    },
    onError: () => toast("Could not create the project", "error"),
  });

  const update = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Parameters<typeof projectsApi.updateProject>[1] }) =>
      projectsApi.updateProject(id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["projects"] });
      setEditing(null);
      toast("Changes saved", "success");
    },
    onError: () => toast("Could not save your changes", "error"),
  });

  const remove = useMutation({
    mutationFn: projectsApi.deleteProject,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["projects"] });
      setDeleting(null);
      toast("Project deleted", "success");
    },
    onError: () => toast("Could not delete the project", "error"),
  });

  const rotate = useMutation({
    mutationFn: projectsApi.rotateKey,
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ["projects"] });
      setRotating(null);
      if (r.api_key) setFreshKey(r.api_key);
      toast("API key rotated", "success");
    },
    onError: () => toast("Could not rotate the API key", "error"),
  });

  const projects = list.data?.projects ?? [];
  const activeCount = projects.filter((p) => p.is_active).length;

  return (
    <div className="space-y-6">
      <PageHead
        title="Projects"
        lede="Each project carries its own API key, rate limit and spend history."
        actions={
          <Button variant="solid" onClick={() => setCreating(true)}>
            <Plus className="h-3.5 w-3.5" aria-hidden />
            New project
          </Button>
        }
      />

      {list.isError ? (
        <Failed title="Could not load projects" onRetry={() => list.refetch()} />
      ) : (
        <>
          {list.isLoading ? (
            <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {Array.from({ length: 6 }).map((_, i) => (
                <li
                  key={i}
                  className="h-[13.5rem] rounded-panel border border-hair bg-panel p-4"
                >
                  <Pulse className="h-4 w-1/2" />
                  <Pulse className="mt-3 h-3 w-2/3" />
                  <Pulse className="mt-6 h-8 w-28" />
                  <Pulse className="mt-6 h-1.5 w-full rounded-full" />
                </li>
              ))}
            </ul>
          ) : projects.length === 0 ? (
            <Panel>
              <Empty
                className="lane-accent"
                icon={FolderKanban}
                title="No projects yet"
                body="A project gets its own key, its own limit and its own line in every report. Create one to start sending requests."
                action={
                  <Button variant="solid" onClick={() => setCreating(true)}>
                    <Plus className="h-3.5 w-3.5" aria-hidden />
                    New project
                  </Button>
                }
              />
            </Panel>
          ) : (
            <>
              <p className="mb-3 text-2xs font-medium uppercase tracking-wide text-text-3">
                {activeCount} of {projects.length} accepting requests
              </p>
              <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                {projects.map((p) => (
                  <ProjectCard
                    key={p.id}
                    project={p}
                    onEdit={() => setEditing(p)}
                    onRotate={() => setRotating(p)}
                    onDelete={() => setDeleting(p)}
                  />
                ))}
              </ul>
            </>
          )}
        </>
      )}

      <CreateSheet
        open={creating}
        onClose={() => setCreating(false)}
        onSubmit={(d) => create.mutate(d)}
        busy={create.isPending}
      />

      {editing && (
        <EditSheet
          project={editing}
          onClose={() => setEditing(null)}
          onSubmit={(d) => update.mutate({ id: editing.id, data: d })}
          busy={update.isPending}
        />
      )}

      {deleting && (
        <Sheet
          open
          onClose={() => setDeleting(null)}
          title={`Delete ${deleting.name}?`}
          description="Its key stops working immediately and its usage history goes with it. This cannot be undone."
        >
          <SheetActions>
            <Button onClick={() => setDeleting(null)}>Keep it</Button>
            <Button variant="danger" busy={remove.isPending} onClick={() => remove.mutate(deleting.id)}>
              Delete project
            </Button>
          </SheetActions>
        </Sheet>
      )}

      {rotating && (
        <Sheet
          open
          onClose={() => setRotating(null)}
          title={`Rotate the key for ${rotating.name}?`}
          description="The current key stops working straight away. Anything still using it starts getting 401s until you update it."
        >
          <SheetActions>
            <Button onClick={() => setRotating(null)}>Cancel</Button>
            <Button variant="solid" busy={rotate.isPending} onClick={() => rotate.mutate(rotating.id)}>
              Rotate key
            </Button>
          </SheetActions>
        </Sheet>
      )}

      {freshKey && <KeySheet apiKey={freshKey} onClose={() => setFreshKey(null)} />}
    </div>
  );
}

/**
 * One project, as a card.
 *
 * The thing people come to this page to compare is spend, so the month's figure is
 * the card's largest element and everything else is arranged around it. The rule
 * along the top takes the colour of the lane the project sends most of its traffic
 * to, which makes a wall of cards sortable by eye before a single number is read.
 */
function ProjectCard({
  project,
  onEdit,
  onRotate,
  onDelete,
}: {
  project: Project;
  onEdit: () => void;
  onRotate: () => void;
  onDelete: () => void;
}) {
  // Spend is what this page is scanned for, so each card fetches its own figures.
  const month = useQuery({
    queryKey: ["project-usage", project.id, "month"],
    queryFn: () => analyticsApi.getProjectUsage(project.id, startOfMonthIso()),
  });
  const life = useQuery({
    queryKey: ["project-usage", project.id, "all"],
    queryFn: () => analyticsApi.getProjectUsage(project.id),
  });

  const loading = month.isLoading || life.isLoading;

  // Split by requests rather than cost: a project living on the free lane spends
  // nothing, and a bar of all zeros would say it had no traffic at all.
  const split = laneSplit(life.data?.by_model ?? []);
  const dominant = split[0]?.lane ?? LANES.other;

  const monthCost = Number(month.data?.summary?.total_cost_usd ?? 0);
  const monthReqs = month.data?.summary?.total_requests ?? 0;
  const lifeCost = Number(life.data?.summary?.total_cost_usd ?? 0);
  const success = life.data?.summary?.success_rate;

  return (
    <li
      className={cn(
        dominant.scope,
        "group relative flex flex-col overflow-hidden rounded-panel border border-hair bg-panel",
        "transition-colors duration-150 hover:border-hair-bright focus-within:border-hair-bright"
      )}
    >
      {/* The lane rule. Thin and flat until the card is hovered, then it owns the edge. */}
      <span
        className="h-0.5 w-full lane-bg opacity-60 transition-opacity duration-150 group-hover:opacity-100"
        aria-hidden
      />

      <div className="flex items-start gap-3 px-4 pt-3.5">
        <div className="min-w-0 flex-1">
          <Link href={`/projects/${project.id}`} className="rounded outline-none">
            <span className="absolute inset-0" aria-hidden />
            {/* Fixed height: the `paused` tag is taller than bare text, and without
                this it nudges the whole card down out of line with its neighbours. */}
            <span className="flex h-5 items-center gap-2">
              <span
                className={cn(
                  "h-1.5 w-1.5 shrink-0 rounded-full",
                  project.is_active ? "bg-ok" : "bg-text-3/50"
                )}
                aria-hidden
              />
              <span className="truncate text-sm font-semibold text-text">{project.name}</span>
              {!project.is_active && <Tag>paused</Tag>}
            </span>
          </Link>
          <p className="mt-1.5 flex items-center gap-2 text-xs text-text-3">
            <code className="font-mono">{project.api_key_prefix}…</code>
            <span aria-hidden>·</span>
            <span className="fig">{project.rate_limit_per_minute}/min</span>
          </p>
        </div>

        <div className="relative z-10 shrink-0">
          <RowMenu name={project.name} onEdit={onEdit} onRotate={onRotate} onDelete={onDelete} />
        </div>
      </div>

      <div className="px-4 pt-4">
        {loading ? (
          <Pulse className="h-8 w-28" />
        ) : (
          <p className="fig text-2xl font-semibold leading-none tracking-tight text-text">
            {money(monthCost)}
          </p>
        )}
        <p className="mt-1.5 text-xs text-text-3">
          {loading ? " " : `this month · ${compact(monthReqs)} requests`}
        </p>
      </div>

      <div className="mt-4 px-4">
        {loading ? (
          <Pulse className="h-1.5 w-full rounded-full" />
        ) : split.length === 0 ? (
          <p className="text-xs text-text-3">No traffic yet</p>
        ) : (
          <>
            {/* Identity comes from the keyed list below, never from hue alone: the
                card's click overlay sits above this bar, so it can carry no hover. */}
            <div className="flex h-1.5 w-full gap-px overflow-hidden rounded-full" aria-hidden>
              {split.map((s) => (
                <span
                  key={s.lane.key}
                  className={cn(s.lane.scope, "h-full lane-bg")}
                  style={{ width: `${s.share}%` }}
                />
              ))}
            </div>
            <ul className="mt-2.5 flex flex-wrap items-center gap-x-3 gap-y-1">
              {split.map((s) => (
                <li key={s.lane.key} className={cn(s.lane.scope, "flex items-center gap-1.5")}>
                  <span className="h-1.5 w-1.5 rounded-full lane-bg" aria-hidden />
                  <span className="text-2xs text-text-3">
                    {s.lane.label} {Math.round(s.share)}%
                  </span>
                </li>
              ))}
            </ul>
          </>
        )}
      </div>

      <div className="mt-4 flex items-center gap-4 border-t border-hair px-4 py-2.5 text-2xs text-text-3">
        <span>
          all time <span className="fig text-text-2">{loading ? "—" : money(lifeCost)}</span>
        </span>
        {!loading && success != null && (
          <span>
            <span className="fig text-text-2">{pct(success, 1)}</span> ok
          </span>
        )}
        <span className="ml-auto hidden truncate sm:block">
          since {fullDate(project.created_at)}
        </span>
        <ChevronRight
          className="h-3.5 w-3.5 shrink-0 text-text-3 transition-transform duration-150 ease-out group-hover:translate-x-0.5"
          aria-hidden
        />
      </div>
    </li>
  );
}

/** Aggregates per-model rows onto lanes, largest share first. */
function laneSplit(byModel: ModelUsageStats[]) {
  const totals = new Map<string, { lane: Lane; requests: number }>();

  for (const row of byModel) {
    const lane = laneOf(row.model_type, row.model_tier);
    const entry = totals.get(lane.key) ?? { lane, requests: 0 };
    entry.requests += row.requests;
    totals.set(lane.key, entry);
  }

  const all = [...totals.values()].filter((e) => e.requests > 0);
  const total = all.reduce((sum, e) => sum + e.requests, 0);
  if (total === 0) return [];

  return all
    .map((e) => ({ ...e, share: (e.requests / total) * 100 }))
    .sort((a, b) => b.share - a.share);
}

function RowMenu({
  name,
  onEdit,
  onRotate,
  onDelete,
}: {
  name: string;
  onEdit: () => void;
  onRotate: () => void;
  onDelete: () => void;
}) {
  return (
    <Menu
      trigger={(props) => (
        <Button {...props} variant="ghost" size="icon" aria-label={`Actions for ${name}`}>
          <MoreVertical className="h-4 w-4" aria-hidden />
        </Button>
      )}
    >
      {(close) => (
        <>
          <MenuItem icon={Pencil} onClick={() => { close(); onEdit(); }}>
            Edit project
          </MenuItem>
          <MenuItem icon={RefreshCw} onClick={() => { close(); onRotate(); }}>
            Rotate API key
          </MenuItem>
          <MenuItem icon={Trash2} tone="danger" onClick={() => { close(); onDelete(); }}>
            Delete project
          </MenuItem>
        </>
      )}
    </Menu>
  );
}

function CreateSheet({
  open,
  onClose,
  onSubmit,
  busy,
}: {
  open: boolean;
  onClose: () => void;
  onSubmit: (d: { name: string; rate_limit_per_minute: number }) => void;
  busy: boolean;
}) {
  const [name, setName] = useState("");
  const [limit, setLimit] = useState("60");

  return (
    <Sheet
      open={open}
      onClose={onClose}
      title="New project"
      description="You get the API key once, immediately after it is created."
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onSubmit({ name, rate_limit_per_minute: parseInt(limit) || 60 });
        }}
        className="space-y-4"
      >
        <Field
          label="Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Mobile app"
          hint="Something you will recognise in the usage logs."
          required
          autoFocus
        />
        <Field
          label="Rate limit"
          type="number"
          value={limit}
          onChange={(e) => setLimit(e.target.value)}
          min={1}
          max={1000}
          hint="Requests per minute."
        />
        <SheetActions>
          <Button type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" variant="solid" busy={busy}>
            Create project
          </Button>
        </SheetActions>
      </form>
    </Sheet>
  );
}

function EditSheet({
  project,
  onClose,
  onSubmit,
  busy,
}: {
  project: Project;
  onClose: () => void;
  onSubmit: (d: { name: string; is_active: boolean; rate_limit_per_minute: number }) => void;
  busy: boolean;
}) {
  const [name, setName] = useState(project.name);
  const [active, setActive] = useState(project.is_active);
  const [limit, setLimit] = useState(project.rate_limit_per_minute.toString());

  return (
    <Sheet open onClose={onClose} title={`Edit ${project.name}`}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onSubmit({ name, is_active: active, rate_limit_per_minute: parseInt(limit) || 60 });
        }}
        className="space-y-4"
      >
        <Field label="Name" value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        <Field
          label="Rate limit"
          type="number"
          value={limit}
          onChange={(e) => setLimit(e.target.value)}
          min={1}
          max={1000}
          hint="Requests per minute."
        />

        <div className="flex items-center justify-between gap-4 rounded border border-hair bg-sunken px-3.5 py-3">
          <span>
            <span className="block text-sm font-medium text-text">Accepting requests</span>
            <span className="block text-xs text-text-3">
              Pausing rejects this key without deleting anything.
            </span>
          </span>
          <button
            type="button"
            role="switch"
            aria-checked={active}
            aria-label="Accepting requests"
            onClick={() => setActive(!active)}
            className={cn(
              "relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors duration-200 ease-out",
              active ? "bg-ok" : "bg-hair-bright"
            )}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 transform rounded-full bg-white transition-transform duration-200 ease-out",
                active ? "translate-x-6" : "translate-x-1"
              )}
            />
          </button>
        </div>

        <SheetActions>
          <Button type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" variant="solid" busy={busy}>
            Save changes
          </Button>
        </SheetActions>
      </form>
    </Sheet>
  );
}

function KeySheet({ apiKey, onClose }: { apiKey: string; onClose: () => void }) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(apiKey);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  return (
    <Sheet open onClose={onClose} title="Copy your API key" description="This is the only time it is shown.">
      <div className="flex items-start gap-2.5 rounded border border-warn/30 bg-warn/[0.08] px-3.5 py-3">
        <TriangleAlert className="mt-px h-4 w-4 shrink-0 text-warn" aria-hidden />
        <p className="text-sm text-text">
          Freeway stores only a hash, so it cannot show you this again. Losing it means rotating.
        </p>
      </div>

      <div className="mt-4 flex items-start gap-2 rounded border border-hair bg-sunken p-3">
        <code className="flex-1 break-all font-mono text-xs text-text">{apiKey}</code>
        <Button size="icon" onClick={copy} aria-label={copied ? "Copied" : "Copy API key"}>
          {copied ? <Check className="h-4 w-4 text-ok" aria-hidden /> : <Copy className="h-4 w-4" aria-hidden />}
        </Button>
      </div>
      <p aria-live="polite" className="sr-only">
        {copied ? "API key copied to clipboard" : ""}
      </p>

      <SheetActions>
        <Button variant="solid" onClick={onClose}>
          I have saved it
        </Button>
      </SheetActions>
    </Sheet>
  );
}
