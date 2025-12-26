import { api } from "./client";
import type { ModelInfo, SelectedModel } from "@/lib/types";

export const modelsApi = {
  // Get selected models
  getSelectedFreeModel: () => api.get<SelectedModel>("/model/free"),
  getSelectedPaidModel: () => api.get<SelectedModel>("/model/paid"),

  // Get all models
  getFreeModels: () => api.get<ModelInfo[]>("/models/free"),
  getPaidModels: () => api.get<ModelInfo[]>("/models/paid"),

  // Set selected models
  setSelectedFreeModel: (modelId: string) =>
    api.put("/admin/model/free", { model_id: modelId }),
  setSelectedPaidModel: (modelId: string) =>
    api.put("/admin/model/paid", { model_id: modelId }),
};
