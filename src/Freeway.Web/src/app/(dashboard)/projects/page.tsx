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
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import { Tag } from "@/components/ui/tag";
import { Sheet, SheetActions } from "@/components/ui/sheet";
import { Pulse, PulseRows } from "@/components/ui/pulse";
import { Empty, Failed } from "@/components/ui/state";
import { useToast } from "@/components/ui/toast";
import { projectsApi } from "@/lib/api/projects";
import { analyticsApi } from "@/lib/api/analytics";
import { cn } from "@/lib/utils/cn";
import { compact, fullDate, money, startOfMonthIso } from "@/lib/utils/format";
import type { Project } from "@/lib/types";

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
        <Panel>
          {list.isLoading ? (
            <PulseRows rows={4} />
          ) : projects.length === 0 ? (
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
          ) : (
            <>
              <div className="flex items-center justify-between border-b border-hair px-4 py-2.5">
                <p className="text-2xs font-medium uppercase text-text-3">
                  {activeCount} of {projects.length} accepting requests
                </p>
                <p className="hidden text-2xs font-medium uppercase text-text-3 sm:block">
                  This month · all time
                </p>
              </div>
              <ul className="divide-y divide-hair">
                {projects.map((p) => (
                  <ProjectRow
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
        </Panel>
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

function ProjectRow({
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
  // Spend is what this list is scanned for, so each row fetches its own figures.
  const month = useQuery({
    queryKey: ["project-usage", project.id, "month"],
    queryFn: () => analyticsApi.getProjectUsage(project.id, startOfMonthIso()),
  });
  const life = useQuery({
    queryKey: ["project-usage", project.id, "all"],
    queryFn: () => analyticsApi.getProjectUsage(project.id),
  });

  const loading = month.isLoading || life.isLoading;

  return (
    <li className="group relative flex items-center gap-4 px-4 py-3 transition-colors hover:bg-raised">
      <span
        className={cn(
          "h-6 w-0.5 shrink-0 rounded-full",
          project.is_active ? "bg-ok" : "bg-text-3/40"
        )}
        aria-hidden
      />

      <Link href={`/projects/${project.id}`} className="min-w-0 flex-1 rounded outline-none">
        <span className="absolute inset-0" aria-hidden />
        <span className="flex flex-wrap items-center gap-2">
          <span className="truncate text-sm font-semibold text-text">{project.name}</span>
          {!project.is_active && <Tag>paused</Tag>}
        </span>
        <span className="mt-0.5 flex flex-wrap items-center gap-x-3 text-xs text-text-3">
          <code className="font-mono">{project.api_key_prefix}…</code>
          <span className="fig">{project.rate_limit_per_minute}/min</span>
          <span className="hidden sm:inline">since {fullDate(project.created_at)}</span>
        </span>
      </Link>

      <div className="relative z-10 flex shrink-0 items-center gap-5">
        <div className="text-right">
          {loading ? (
            <Pulse className="ml-auto h-4 w-16" />
          ) : (
            <p className="fig text-sm text-text">
              {money(Number(month.data?.summary?.total_cost_usd ?? 0))}
            </p>
          )}
          <p className="text-2xs text-text-3">
            {loading ? "" : `${compact(month.data?.summary?.total_requests ?? 0)} req`}
          </p>
        </div>
        <div className="hidden text-right sm:block">
          {loading ? (
            <Pulse className="ml-auto h-4 w-16" />
          ) : (
            <p className="fig text-sm text-text-2">
              {money(Number(life.data?.summary?.total_cost_usd ?? 0))}
            </p>
          )}
          <p className="text-2xs text-text-3">
            {loading ? "" : `${compact(life.data?.summary?.total_requests ?? 0)} req`}
          </p>
        </div>

        <RowMenu name={project.name} onEdit={onEdit} onRotate={onRotate} onDelete={onDelete} />

        <ChevronRight
          className="hidden h-4 w-4 text-text-3 transition-transform duration-150 ease-out group-hover:translate-x-0.5 lg:block"
          aria-hidden
        />
      </div>
    </li>
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
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);

  const items = [
    { label: "Edit project", icon: Pencil, run: onEdit, tone: "" },
    { label: "Rotate API key", icon: RefreshCw, run: onRotate, tone: "" },
    { label: "Delete project", icon: Trash2, run: onDelete, tone: "text-bad" },
  ];

  return (
    <div className="relative" ref={ref}>
      <Button
        variant="ghost"
        size="icon"
        aria-label={`Actions for ${name}`}
        aria-expanded={open}
        aria-haspopup="menu"
        onClick={() => setOpen((v) => !v)}
      >
        <MoreVertical className="h-4 w-4" aria-hidden />
      </Button>
      {open && (
        <>
          <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} aria-hidden />
          <div
            role="menu"
            className="absolute right-0 top-full z-20 mt-1 w-44 overflow-hidden rounded border border-hair-bright bg-raised py-1 animate-pop"
          >
            {items.map((i) => (
              <button
                key={i.label}
                role="menuitem"
                onClick={() => {
                  setOpen(false);
                  i.run();
                }}
                className={cn(
                  "flex w-full items-center gap-2.5 px-3 py-2 text-sm transition-colors hover:bg-panel",
                  i.tone || "text-text-2 hover:text-text"
                )}
              >
                <i.icon className="h-3.5 w-3.5" aria-hidden />
                {i.label}
              </button>
            ))}
          </div>
        </>
      )}
    </div>
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
