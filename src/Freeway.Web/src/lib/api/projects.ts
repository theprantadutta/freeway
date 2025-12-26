import { api } from "./client";
import type {
  Project,
  ProjectWithKey,
  CreateProjectRequest,
  UpdateProjectRequest,
  RotateKeyResult,
} from "@/lib/types";

export const projectsApi = {
  // List all projects
  getProjects: () =>
    api.get<{ projects: Project[]; total_count: number }>("/admin/projects"),

  // Get single project
  getProject: (id: string) => api.get<Project>(`/admin/projects/${id}`),

  // Create project
  createProject: (data: CreateProjectRequest) =>
    api.post<ProjectWithKey>("/admin/projects", data),

  // Update project
  updateProject: (id: string, data: UpdateProjectRequest) =>
    api.patch<Project>(`/admin/projects/${id}`, data),

  // Delete project
  deleteProject: (id: string) => api.delete(`/admin/projects/${id}`),

  // Rotate API key
  rotateKey: (id: string) =>
    api.post<RotateKeyResult>(`/admin/projects/${id}/rotate-key`),

  // Refresh cache
  refreshCache: () => api.post("/admin/projects/refresh-cache"),
};
