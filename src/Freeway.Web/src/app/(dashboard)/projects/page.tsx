"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Plus,
  FolderKanban,
  Key,
  Gauge,
  MoreVertical,
  Pencil,
  RefreshCw,
  Trash2,
  Copy,
  Check,
  ChevronRight,
  TriangleAlert,
} from "lucide-react";
import { Header } from "@/components/layout/header";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogActions } from "@/components/ui/dialog";
import { Skeleton, SkeletonList } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { projectsApi } from "@/lib/api/projects";
import { analyticsApi } from "@/lib/api/analytics";
import {
  formatDate,
  formatNumber,
  formatSpend,
  startOfMonthIso,
} from "@/lib/utils/format";
import { cn } from "@/lib/utils/cn";
import type { Project } from "@/lib/types";

export default function ProjectsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [editingProject, setEditingProject] = useState<Project | null>(null);
  const [deleteProject, setDeleteProject] = useState<Project | null>(null);
  const [rotateKeyProject, setRotateKeyProject] = useState<Project | null>(null);
  const [newApiKey, setNewApiKey] = useState<string | null>(null);

  const projectsQuery = useQuery({
    queryKey: ["projects"],
    queryFn: () => projectsApi.getProjects(),
  });

  const createMutation = useMutation({
    mutationFn: projectsApi.createProject,
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      setShowCreateDialog(false);
      if (result.api_key) setNewApiKey(result.api_key);
      toast("Project created", "success");
    },
    onError: () => toast("Could not create the project", "error"),
  });

  const updateMutation = useMutation({
    mutationFn: ({
      id,
      data,
    }: {
      id: string;
      data: Parameters<typeof projectsApi.updateProject>[1];
    }) => projectsApi.updateProject(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      setEditingProject(null);
      toast("Changes saved", "success");
    },
    onError: () => toast("Could not save your changes", "error"),
  });

  const deleteMutation = useMutation({
    mutationFn: projectsApi.deleteProject,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      setDeleteProject(null);
      toast("Project deleted", "success");
    },
    onError: () => toast("Could not delete the project", "error"),
  });

  const rotateKeyMutation = useMutation({
    mutationFn: projectsApi.rotateKey,
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      setRotateKeyProject(null);
      if (result.api_key) setNewApiKey(result.api_key);
      toast("API key rotated", "success");
    },
    onError: () => toast("Could not rotate the API key", "error"),
  });

  const projects = projectsQuery.data?.projects || [];
  const activeCount = projects.filter((p) => p.is_active).length;

  return (
    <div className="flex h-full flex-col">
      <Header
        title="Projects"
        subtitle={
          projects.length
            ? `${activeCount} of ${projects.length} active`
            : "API keys and rate limits"
        }
        actions={
          <Button onClick={() => setShowCreateDialog(true)}>
            <Plus className="h-4 w-4" aria-hidden />
            New project
          </Button>
        }
      />

      <div className="flex-1 space-y-3 p-4 md:p-6">
        {projectsQuery.isLoading ? (
          <SkeletonList count={3} />
        ) : projectsQuery.isError ? (
          <ErrorState
            title="Could not load projects"
            description="The gateway did not respond. Check that the API is reachable and try again."
            onRetry={() => projectsQuery.refetch()}
          />
        ) : projects.length === 0 ? (
          <EmptyState
            className="accent-brand"
            icon={FolderKanban}
            title="No projects yet"
            description="A project gets its own API key, rate limit and usage history. Create one to start sending requests."
            action={
              <Button onClick={() => setShowCreateDialog(true)}>
                <Plus className="h-4 w-4" aria-hidden />
                New project
              </Button>
            }
          />
        ) : (
          <div className="stagger grid gap-2.5">
            {projects.map((project) => (
              <ProjectRow
                key={project.id}
                project={project}
                onEdit={() => setEditingProject(project)}
                onRotateKey={() => setRotateKeyProject(project)}
                onDelete={() => setDeleteProject(project)}
              />
            ))}
          </div>
        )}
      </div>

      <CreateProjectDialog
        isOpen={showCreateDialog}
        onClose={() => setShowCreateDialog(false)}
        onSubmit={(data) => createMutation.mutate(data)}
        isLoading={createMutation.isPending}
      />

      {editingProject && (
        <EditProjectDialog
          project={editingProject}
          onClose={() => setEditingProject(null)}
          onSubmit={(data) =>
            updateMutation.mutate({ id: editingProject.id, data })
          }
          isLoading={updateMutation.isPending}
        />
      )}

      {deleteProject && (
        <Dialog
          isOpen
          onClose={() => setDeleteProject(null)}
          title={`Delete ${deleteProject.name}?`}
          description="Its API key stops working immediately and its usage history is removed. This cannot be undone."
        >
          <DialogActions>
            <Button variant="outline" onClick={() => setDeleteProject(null)}>
              Keep project
            </Button>
            <Button
              variant="danger"
              onClick={() => deleteMutation.mutate(deleteProject.id)}
              isLoading={deleteMutation.isPending}
            >
              Delete project
            </Button>
          </DialogActions>
        </Dialog>
      )}

      {rotateKeyProject && (
        <Dialog
          isOpen
          onClose={() => setRotateKeyProject(null)}
          title={`Rotate the key for ${rotateKeyProject.name}?`}
          description="The current key stops working straight away. Anything still using it will start getting 401s until you update it."
        >
          <DialogActions>
            <Button variant="outline" onClick={() => setRotateKeyProject(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => rotateKeyMutation.mutate(rotateKeyProject.id)}
              isLoading={rotateKeyMutation.isPending}
            >
              Rotate key
            </Button>
          </DialogActions>
        </Dialog>
      )}

      {newApiKey && (
        <ApiKeyDialog apiKey={newApiKey} onClose={() => setNewApiKey(null)} />
      )}
    </div>
  );
}

function ProjectRow({
  project,
  onEdit,
  onRotateKey,
  onDelete,
}: {
  project: Project;
  onEdit: () => void;
  onRotateKey: () => void;
  onDelete: () => void;
}) {
  // Spend is the number the operator actually scans this list for, so each row
  // fetches its own month-to-date and lifetime totals.
  const monthUsage = useQuery({
    queryKey: ["project-usage", project.id, "month"],
    queryFn: () => analyticsApi.getProjectUsage(project.id, startOfMonthIso()),
  });

  const lifetimeUsage = useQuery({
    queryKey: ["project-usage", project.id, "all"],
    queryFn: () => analyticsApi.getProjectUsage(project.id),
  });

  const month = monthUsage.data?.summary;
  const lifetime = lifetimeUsage.data?.summary;
  const loadingSpend = monthUsage.isLoading || lifetimeUsage.isLoading;

  return (
    <Card className="group relative px-4 py-3.5 transition-colors duration-200 ease-swift hover:border-line-strong">
      <div className="flex items-start gap-4">
        <span
          className={cn(
            "mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-control",
            project.is_active ? "bg-ok/12 text-ok" : "bg-inset text-subtle"
          )}
        >
          <FolderKanban className="h-4 w-4" aria-hidden />
        </span>

        <Link
          href={`/projects/${project.id}`}
          className="min-w-0 flex-1 rounded-control outline-none"
        >
          {/* Stretches the link across the row so the whole card is clickable. */}
          <span className="absolute inset-0" aria-hidden />
          <span className="flex flex-wrap items-center gap-2">
            <span className="truncate text-base font-semibold text-ink">
              {project.name}
            </span>
            <Badge variant={project.is_active ? "success" : "default"} dot>
              {project.is_active ? "Active" : "Paused"}
            </Badge>
          </span>
          <span className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-subtle">
            <span className="inline-flex items-center gap-1.5">
              <Key className="h-3 w-3" aria-hidden />
              <code className="font-mono">{project.api_key_prefix}…</code>
            </span>
            <span className="inline-flex items-center gap-1.5">
              <Gauge className="h-3 w-3" aria-hidden />
              <span className="tnum">{project.rate_limit_per_minute}</span>/min
            </span>
            <span>Created {formatDate(project.created_at)}</span>
          </span>

          {/* Spend, inline on small screens where the column layout does not fit. */}
          <span className="mt-2 flex items-center gap-4 sm:hidden">
            <span className="text-xs text-subtle">
              This month{" "}
              <span className="metric font-semibold text-ink">
                {loadingSpend ? "…" : formatSpend(month?.total_cost_usd)}
              </span>
            </span>
            <span className="text-xs text-subtle">
              All time{" "}
              <span className="metric font-semibold text-ink">
                {loadingSpend ? "…" : formatSpend(lifetime?.total_cost_usd)}
              </span>
            </span>
          </span>
        </Link>

        <dl className="hidden shrink-0 items-start gap-6 sm:flex">
          <div className="text-right">
            <dt className="text-xs text-subtle">This month</dt>
            <dd className="metric mt-0.5 text-sm font-semibold text-ink">
              {loadingSpend ? (
                <Skeleton className="ml-auto h-4 w-14" />
              ) : (
                formatSpend(month?.total_cost_usd)
              )}
            </dd>
          </div>
          <div className="text-right">
            <dt className="text-xs text-subtle">All time</dt>
            <dd className="metric mt-0.5 text-sm font-semibold text-ink">
              {loadingSpend ? (
                <Skeleton className="ml-auto h-4 w-14" />
              ) : (
                formatSpend(lifetime?.total_cost_usd)
              )}
            </dd>
          </div>
          <div className="hidden text-right lg:block">
            <dt className="text-xs text-subtle">Requests</dt>
            <dd className="metric mt-0.5 text-sm font-semibold text-ink">
              {loadingSpend ? (
                <Skeleton className="ml-auto h-4 w-12" />
              ) : (
                formatNumber(lifetime?.total_requests ?? 0)
              )}
            </dd>
          </div>
        </dl>

        <ChevronRight
          className="mt-2 hidden h-4 w-4 shrink-0 text-subtle transition-transform duration-200 ease-swift group-hover:translate-x-0.5 sm:block"
          aria-hidden
        />

        <RowMenu
          projectName={project.name}
          onEdit={onEdit}
          onRotateKey={onRotateKey}
          onDelete={onDelete}
        />
      </div>
    </Card>
  );
}

function RowMenu({
  projectName,
  onEdit,
  onRotateKey,
  onDelete,
}: {
  projectName: string;
  onEdit: () => void;
  onRotateKey: () => void;
  onDelete: () => void;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);

  const items = [
    { label: "Edit project", icon: Pencil, run: onEdit, tone: "" },
    { label: "Rotate API key", icon: RefreshCw, run: onRotateKey, tone: "" },
    { label: "Delete project", icon: Trash2, run: onDelete, tone: "text-danger" },
  ];

  return (
    <div className="relative z-10 shrink-0" ref={ref}>
      <Button
        variant="ghost"
        size="icon"
        aria-label={`Actions for ${projectName}`}
        aria-expanded={open}
        aria-haspopup="menu"
        onClick={() => setOpen((v) => !v)}
      >
        <MoreVertical className="h-4 w-4" aria-hidden />
      </Button>

      {open && (
        <>
          <div
            className="fixed inset-0 z-10"
            onClick={() => setOpen(false)}
            aria-hidden
          />
          <div
            role="menu"
            className="absolute right-0 top-full z-20 mt-1 min-w-[11rem] overflow-hidden rounded-panel border border-line bg-panel py-1 shadow-pop animate-scale-in"
          >
            {items.map((item) => (
              <button
                key={item.label}
                role="menuitem"
                onClick={() => {
                  setOpen(false);
                  item.run();
                }}
                className={cn(
                  "flex w-full items-center gap-2.5 px-3 py-2 text-sm transition-colors hover:bg-inset",
                  item.tone || "text-ink"
                )}
              >
                <item.icon className="h-3.5 w-3.5" aria-hidden />
                {item.label}
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

function CreateProjectDialog({
  isOpen,
  onClose,
  onSubmit,
  isLoading,
}: {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: { name: string; rate_limit_per_minute: number }) => void;
  isLoading: boolean;
}) {
  const [name, setName] = useState("");
  const [rateLimit, setRateLimit] = useState("60");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({ name, rate_limit_per_minute: parseInt(rateLimit) || 60 });
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title="New project"
      description="You will get an API key once, right after it is created."
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Mobile app"
          hint="Something you will recognise in the usage logs."
          required
          autoFocus
        />
        <Input
          label="Rate limit"
          type="number"
          value={rateLimit}
          onChange={(e) => setRateLimit(e.target.value)}
          min={1}
          max={1000}
          hint="Requests per minute."
        />
        <DialogActions>
          <Button type="button" variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isLoading}>
            Create project
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

function EditProjectDialog({
  project,
  onClose,
  onSubmit,
  isLoading,
}: {
  project: Project;
  onClose: () => void;
  onSubmit: (data: {
    name: string;
    is_active: boolean;
    rate_limit_per_minute: number;
  }) => void;
  isLoading: boolean;
}) {
  const [name, setName] = useState(project.name);
  const [isActive, setIsActive] = useState(project.is_active);
  const [rateLimit, setRateLimit] = useState(
    project.rate_limit_per_minute.toString()
  );

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      name,
      is_active: isActive,
      rate_limit_per_minute: parseInt(rateLimit) || 60,
    });
  };

  return (
    <Dialog isOpen onClose={onClose} title={`Edit ${project.name}`}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          autoFocus
        />
        <Input
          label="Rate limit"
          type="number"
          value={rateLimit}
          onChange={(e) => setRateLimit(e.target.value)}
          min={1}
          max={1000}
          hint="Requests per minute."
        />

        <div className="flex items-center justify-between gap-4 rounded-control border border-line bg-inset px-3.5 py-3">
          <span>
            <span className="block text-sm font-medium text-ink">Accepting requests</span>
            <span className="block text-xs text-subtle">
              Pausing rejects this project&rsquo;s key without deleting anything.
            </span>
          </span>
          <button
            type="button"
            role="switch"
            aria-checked={isActive}
            aria-label="Accepting requests"
            onClick={() => setIsActive(!isActive)}
            className={cn(
              "relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors duration-200 ease-swift",
              isActive ? "bg-ok" : "bg-line-strong"
            )}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 transform rounded-full bg-white transition-transform duration-200 ease-swift",
                isActive ? "translate-x-6" : "translate-x-1"
              )}
            />
          </button>
        </div>

        <DialogActions>
          <Button type="button" variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isLoading}>
            Save changes
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

function ApiKeyDialog({
  apiKey,
  onClose,
}: {
  apiKey: string;
  onClose: () => void;
}) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(apiKey);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  return (
    <Dialog
      isOpen
      onClose={onClose}
      title="Copy your API key"
      description="This is the only time it is shown."
    >
      <div className="flex items-start gap-2.5 rounded-control border border-warn/30 bg-warn/[0.08] px-3.5 py-3">
        <TriangleAlert className="mt-px h-4 w-4 shrink-0 text-warn" aria-hidden />
        <p className="text-sm text-ink">
          Store it somewhere safe now. Freeway keeps only a hash, so it cannot show
          you this key again — you would have to rotate it.
        </p>
      </div>

      <div className="mt-4 flex items-center gap-2 rounded-control border border-line bg-inset px-3 py-2.5">
        <code className="flex-1 break-all font-mono text-sm text-ink">{apiKey}</code>
        <Button
          variant="ghost"
          size="icon"
          onClick={handleCopy}
          aria-label={copied ? "Copied" : "Copy API key"}
        >
          {copied ? (
            <Check className="h-4 w-4 text-ok" aria-hidden />
          ) : (
            <Copy className="h-4 w-4" aria-hidden />
          )}
        </Button>
      </div>
      <p aria-live="polite" className="sr-only">
        {copied ? "API key copied to clipboard" : ""}
      </p>

      <DialogActions>
        <Button onClick={onClose}>I have saved it</Button>
      </DialogActions>
    </Dialog>
  );
}
