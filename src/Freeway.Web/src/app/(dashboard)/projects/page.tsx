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
import { LaneDot, Sparkline } from "@/components/charts/primitives";
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
import { cents, count, fullDate, ms } from "@/lib/utils/format";
import { laneOf } from "@/lib/theme/lanes";
import type { Project, TopProject } from "@/lib/types";

export default function ProjectsPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Project | null>(null);
  const [deleting, setDeleting] = useState<Project | null>(null);
  const [rotating, setRotating] = useState<Project | null>(null);
  const [freshKey, setFreshKey] = useState<string | null>(null);

  const list = useQuery({ queryKey: ["projects"], queryFn: () => projectsApi.getProjects() });
  // One call gives every project its 30-day figures and trend.
  const overview = useQuery({ queryKey: ["overview", "30"], queryFn: () => analyticsApi.getOverview(30) });
  const stats = new Map((overview.data?.top_projects ?? []).map((p) => [p.project_id, p]));

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
    <div className="space-y-8">
      <PageHead
        title="Projects"
        lede="Each app that calls Freeway is a project, with its own key, rate limit and history. Figures cover the last 30 days."
        actions={
          <Button variant="solid" onClick={() => setCreating(true)}>
            <Plus className="h-4 w-4" aria-hidden />
            New project
          </Button>
        }
      />

      {list.isError ? (
        <Failed title="Could not load projects" onRetry={() => list.refetch()} />
      ) : (
        <>
          {list.isLoading ? (
            <ul className="grid gap-5 sm:grid-cols-2 2xl:grid-cols-3">
              {Array.from({ length: 6 }).map((_, i) => (
                <li
                  key={i}
                  className="card h-[17rem] p-6"
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
                    <Plus className="h-4 w-4" aria-hidden />
                    New project
                  </Button>
                }
              />
            </Panel>
          ) : (
            <>
              <p className="reveal mb-4 text-base text-text-3">
                {activeCount} of {projects.length} accepting requests
              </p>
              <ul className="grid gap-5 sm:grid-cols-2 2xl:grid-cols-3">
                {[...projects]
                  .sort((a, b) => (stats.get(b.id)?.requests ?? 0) - (stats.get(a.id)?.requests ?? 0))
                  .map((p, i) => (
                  <ProjectCard
                    key={p.id}
                    index={i}
                    project={p}
                    stats={stats.get(p.id)}
                    loading={overview.isLoading}
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
 * One project, as a card. Requests lead, because they are what changes day to day;
 * money sits below in its two kinds, credit and billed, and the trend along the
 * bottom says whether the app is getting busier.
 */
function ProjectCard({
  project,
  stats,
  loading,
  index,
  onEdit,
  onRotate,
  onDelete,
}: {
  project: Project;
  stats?: TopProject;
  loading: boolean;
  index: number;
  onEdit: () => void;
  onRotate: () => void;
  onDelete: () => void;
}) {
  const lane = laneOf("paid", stats?.top_lane);
  const success = stats && stats.requests ? ((stats.requests - (stats.failures ?? 0)) / stats.requests) * 100 : null;

  return (
    <li
      className={cn(
        "card reveal group relative flex flex-col overflow-hidden",
        "transition-[border-color,transform] duration-300 ease-out hover:-translate-y-0.5 hover:border-hair-bright focus-within:border-hair-bright"
      )}
      style={{ ["--d" as string]: `${Math.min(index * 50, 400)}ms` }}
    >
      <div className="flex items-start gap-3 px-6 pt-6">
        <div className="min-w-0 flex-1">
          <Link href={`/projects/${project.id}`} className="rounded outline-none">
            <span className="absolute inset-0" aria-hidden />
            <span className="flex items-center gap-2.5">
              <span
                className={cn("h-2.5 w-2.5 shrink-0 rounded-full", project.is_active ? "bg-ok" : "bg-text-3/50")}
                aria-hidden
              />
              <span className="truncate text-xl font-bold text-text group-hover:underline">{project.name}</span>
              {!project.is_active && <Tag>paused</Tag>}
            </span>
          </Link>
          <p className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-text-3">
            <code className="font-mono">{project.api_key_prefix}…</code>
            <span className="tnum">{project.rate_limit_per_minute} per minute</span>
          </p>
        </div>

        <div className="relative z-10 shrink-0">
          <RowMenu name={project.name} onEdit={onEdit} onRotate={onRotate} onDelete={onDelete} />
        </div>
      </div>

      <div className="mt-6 grid grid-cols-3 gap-4 px-6">
        <Figure label="Requests" value={loading ? null : count(stats?.requests ?? 0)} big />
        <Figure label="Credit" value={loading ? null : cents(Number(stats?.credit_usd ?? 0))} />
        <Figure label="Billed" value={loading ? null : cents(Number(stats?.cost ?? 0))} />
      </div>

      <div className="mt-5 px-6">
        {loading ? (
          <Pulse className="h-9 w-full" />
        ) : stats ? (
          <Sparkline values={stats.daily ?? []} height={36} emphasis />
        ) : (
          <p className="py-2 text-sm text-text-3">No requests in the last 30 days</p>
        )}
      </div>

      <div className="mt-auto flex items-center gap-4 border-t border-hair px-6 py-3.5 text-sm text-text-3">
        {stats ? (
          <>
            <span className="flex items-center gap-2">
              <LaneDot scope={lane.scope} className="h-2 w-2" />
              Mostly {lane.label.toLowerCase()}
            </span>
            {success != null && <span className="tnum">{success.toFixed(1)}% ok</span>}
            <span className="tnum hidden sm:inline">{ms(stats.avg_response_ms)}</span>
          </>
        ) : (
          <span>Since {fullDate(project.created_at)}</span>
        )}
        <ChevronRight
          className="ml-auto h-4 w-4 shrink-0 text-text-3 transition-transform duration-200 ease-out group-hover:translate-x-1"
          aria-hidden
        />
      </div>
    </li>
  );
}

function Figure({ label, value, big }: { label: string; value: string | null; big?: boolean }) {
  return (
    <div className="min-w-0">
      <p className="text-sm text-text-3">{label}</p>
      {value == null ? (
        <Pulse className="mt-1.5 h-7 w-16" />
      ) : (
        <p className={cn("num mt-1 truncate font-extrabold text-text", big ? "text-2xl" : "text-xl")}>{value}</p>
      )}
    </div>
  );
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
          <MoreVertical className="h-5 w-5" aria-hidden />
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

        <div className="flex items-center justify-between gap-4 rounded-xl border border-hair bg-sunken px-4 py-3.5">
          <span>
            <span className="block text-base font-semibold text-text">Accepting requests</span>
            <span className="block text-sm text-text-3">
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
      <div className="flex items-start gap-3 rounded-xl border border-warn/30 bg-warn/[0.08] px-4 py-3.5">
        <TriangleAlert className="mt-0.5 h-5 w-5 shrink-0 text-warn" aria-hidden />
        <p className="text-base text-text">
          Freeway stores only a hash, so it cannot show you this again. Losing it means rotating.
        </p>
      </div>

      <div className="mt-4 flex items-start gap-2 rounded-xl border border-hair bg-sunken p-3.5">
        <code className="flex-1 break-all font-mono text-sm text-text">{apiKey}</code>
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
