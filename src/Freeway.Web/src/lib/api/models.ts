import { api } from "./client";
import type { ModelInfo, SelectedModel } from "@/lib/types";

// Backend returns { models: [...], total_count, last_updated }
interface ModelsListResponse {
  models: ModelInfo[];
  total_count: number;
  last_updated: string;
}

export const modelsApi = {
  // Get selected models
  getSelectedFreeModel: () => api.get<SelectedModel>("/model/free"),
  getSelectedPaidModel: () => api.get<SelectedModel>("/model/paid"),

  // Get all models - extract models array from response
  getFreeModels: async () => {
    const response = await api.get<ModelsListResponse>("/models/free");
    return response.models || [];
  },
  getPaidModels: async () => {
    const response = await api.get<ModelsListResponse>("/models/paid");
    return response.models || [];
  },

  // Set selected models
  setSelectedFreeModel: (modelId: string) =>
    api.put("/admin/model/free", { model_id: modelId }),
  setSelectedPaidModel: (modelId: string) =>
    api.put("/admin/model/paid", { model_id: modelId }),
};
