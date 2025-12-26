"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Plus,
  FolderKanban,
  Key,
  Gauge,
  Calendar,
  MoreVertical,
  Edit,
  RefreshCw,
  Trash2,
  Copy,
  Check,
} from "lucide-react";
import { Header } from "@/components/layout/header";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogActions } from "@/components/ui/dialog";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { projectsApi } from "@/lib/api/projects";
import { formatDate } from "@/lib/utils/format";
import type { Project } from "@/lib/types";

export default function ProjectsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [editingProject, setEditingProject] = useState<Project | null>(null);
  const [deleteProject, setDeleteProject] = useState<Project | null>(null);
  const [rotateKeyProject, setRotateKeyProject] = useState<Project | null>(
    null
  );
  const [newApiKey, setNewApiKey] = useState<string | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["projects"],
    queryFn: () => projectsApi.getProjects(),
  });

  const createMutation = useMutation({
    mutationFn: projectsApi.createProject,
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      setShowCreateDialog(false);
      if (result.api_key) {
        setNewApiKey(result.api_key);
      }
      toast("Project created successfully", "success");
    },
    onError: () => {
      toast("Failed to create project", "error");
    },
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
      toast("Project updated successfully", "success");
    },
    onError: () => {
      toast("Failed to update project", "error");
    },
  });

  const deleteMutation = useMutation({
    mutationFn: projectsApi.deleteProject,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      setDeleteProject(null);
      toast("Project deleted successfully", "success");
    },
    onError: () => {
      toast("Failed to delete project", "error");
    },
  });

  const rotateKeyMutation = useMutation({
    mutationFn: projectsApi.rotateKey,
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      setRotateKeyProject(null);
      if (result.api_key) {
        setNewApiKey(result.api_key);
      }
      toast("API key rotated successfully", "success");
    },
    onError: () => {
      toast("Failed to rotate API key", "error");
    },
  });

  const projects = data?.projects || [];

  return (
    <div className="flex flex-col h-full">
      <Header title="Projects" subtitle="Manage your API projects" />

      <div className="flex-1 p-4 md:p-6 space-y-4">
        {/* Actions */}
        <div className="flex justify-end">
          <Button onClick={() => setShowCreateDialog(true)}>
            <Plus className="h-4 w-4 mr-2" />
            Create Project
          </Button>
        </div>

        {/* Projects List */}
        {isLoading ? (
          <SkeletonList count={3} />
        ) : projects.length === 0 ? (
          <EmptyState
            icon={FolderKanban}
            title="No projects yet"
            description="Create your first project to get started with the API"
            action={
              <Button onClick={() => setShowCreateDialog(true)}>
                <Plus className="h-4 w-4 mr-2" />
                Create Project
              </Button>
            }
          />
        ) : (
          <div className="grid gap-4">
            {projects.map((project) => (
              <ProjectCard
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

      {/* Create Dialog */}
      <CreateProjectDialog
        isOpen={showCreateDialog}
        onClose={() => setShowCreateDialog(false)}
        onSubmit={(data) => createMutation.mutate(data)}
        isLoading={createMutation.isPending}
      />

      {/* Edit Dialog */}
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

      {/* Delete Confirmation */}
      {deleteProject && (
        <Dialog
          isOpen={true}
          onClose={() => setDeleteProject(null)}
          title="Delete Project"
          description={`Are you sure you want to delete "${deleteProject.name}"? This action cannot be undone.`}
        >
          <DialogActions>
            <Button variant="outline" onClick={() => setDeleteProject(null)}>
              Cancel
            </Button>
            <Button
              variant="danger"
              onClick={() => deleteMutation.mutate(deleteProject.id)}
              isLoading={deleteMutation.isPending}
            >
              Delete
            </Button>
          </DialogActions>
        </Dialog>
      )}

      {/* Rotate Key Confirmation */}
      {rotateKeyProject && (
        <Dialog
          isOpen={true}
          onClose={() => setRotateKeyProject(null)}
          title="Rotate API Key"
          description={`This will invalidate the current API key for "${rotateKeyProject.name}" and generate a new one. Any applications using the old key will stop working.`}
        >
          <DialogActions>
            <Button variant="outline" onClick={() => setRotateKeyProject(null)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              onClick={() => rotateKeyMutation.mutate(rotateKeyProject.id)}
              isLoading={rotateKeyMutation.isPending}
            >
              Rotate Key
            </Button>
          </DialogActions>
        </Dialog>
      )}

      {/* New API Key Display */}
      {newApiKey && (
        <ApiKeyDialog apiKey={newApiKey} onClose={() => setNewApiKey(null)} />
      )}
    </div>
  );
}

function ProjectCard({
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
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <Card hover>
      <CardContent className="py-4">
        <div className="flex items-start gap-4">
          <div className="p-2.5 bg-primary-100 dark:bg-primary-900/30 rounded-lg">
            <FolderKanban className="h-5 w-5 text-primary-600 dark:text-primary-400" />
          </div>

          <Link href={`/projects/${project.id}`} className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="font-medium text-gray-900 dark:text-gray-100">
                {project.name}
              </h3>
              <Badge variant={project.is_active ? "success" : "default"}>
                {project.is_active ? "Active" : "Inactive"}
              </Badge>
            </div>

            <div className="flex flex-wrap gap-4 mt-2 text-xs text-gray-500 dark:text-gray-400">
              <span className="flex items-center gap-1">
                <Key className="h-3 w-3" />
                {project.api_key_prefix}...
              </span>
              <span className="flex items-center gap-1">
                <Gauge className="h-3 w-3" />
                {project.rate_limit_per_minute}/min
              </span>
              <span className="flex items-center gap-1">
                <Calendar className="h-3 w-3" />
                {formatDate(project.created_at)}
              </span>
            </div>
          </Link>

          <div className="relative">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setMenuOpen(!menuOpen)}
              className="p-2"
            >
              <MoreVertical className="h-4 w-4" />
            </Button>

            {menuOpen && (
              <>
                <div
                  className="fixed inset-0 z-10"
                  onClick={() => setMenuOpen(false)}
                />
                <div className="absolute right-0 top-full mt-1 z-20 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg shadow-lg py-1 min-w-[140px]">
                  <button
                    onClick={() => {
                      setMenuOpen(false);
                      onEdit();
                    }}
                    className="flex items-center gap-2 w-full px-3 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800"
                  >
                    <Edit className="h-4 w-4" />
                    Edit
                  </button>
                  <button
                    onClick={() => {
                      setMenuOpen(false);
                      onRotateKey();
                    }}
                    className="flex items-center gap-2 w-full px-3 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800"
                  >
                    <RefreshCw className="h-4 w-4" />
                    Rotate Key
                  </button>
                  <button
                    onClick={() => {
                      setMenuOpen(false);
                      onDelete();
                    }}
                    className="flex items-center gap-2 w-full px-3 py-2 text-sm text-red-600 dark:text-red-400 hover:bg-gray-100 dark:hover:bg-gray-800"
                  >
                    <Trash2 className="h-4 w-4" />
                    Delete
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
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
    onSubmit({
      name,
      rate_limit_per_minute: parseInt(rateLimit) || 60,
    });
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title="Create Project">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Project Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="My Project"
          required
        />
        <Input
          label="Rate Limit (per minute)"
          type="number"
          value={rateLimit}
          onChange={(e) => setRateLimit(e.target.value)}
          min={1}
          max={1000}
        />
        <DialogActions>
          <Button type="button" variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isLoading}>
            Create
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
    <Dialog isOpen={true} onClose={onClose} title="Edit Project">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Project Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
        />
        <Input
          label="Rate Limit (per minute)"
          type="number"
          value={rateLimit}
          onChange={(e) => setRateLimit(e.target.value)}
          min={1}
          max={1000}
        />
        <div className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
          <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
            Project Status
          </span>
          <button
            type="button"
            onClick={() => setIsActive(!isActive)}
            className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
              isActive ? "bg-green-500" : "bg-gray-300 dark:bg-gray-600"
            }`}
          >
            <span
              className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                isActive ? "translate-x-6" : "translate-x-1"
              }`}
            />
          </button>
        </div>
        <DialogActions>
          <Button type="button" variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isLoading}>
            Save
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

  const handleCopy = () => {
    navigator.clipboard.writeText(apiKey);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Dialog
      isOpen={true}
      onClose={onClose}
      title="Save Your API Key"
      description="This key will only be shown once. Make sure to copy it now."
    >
      <div className="p-3 bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg mb-4">
        <p className="text-sm text-yellow-800 dark:text-yellow-200">
          Warning: This key will not be shown again!
        </p>
      </div>

      <div className="flex items-center gap-2 p-3 bg-gray-100 dark:bg-gray-800 rounded-lg font-mono text-sm">
        <code className="flex-1 break-all text-gray-900 dark:text-gray-100">
          {apiKey}
        </code>
        <Button variant="ghost" size="sm" onClick={handleCopy}>
          {copied ? (
            <Check className="h-4 w-4 text-green-500" />
          ) : (
            <Copy className="h-4 w-4" />
          )}
        </Button>
      </div>

      <DialogActions>
        <Button onClick={onClose}>I have saved the key</Button>
      </DialogActions>
    </Dialog>
  );
}
