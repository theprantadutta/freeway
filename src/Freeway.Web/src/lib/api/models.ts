import { api } from "./client";
import type { ModelInfo, PaidTier, SelectedModel } from "@/lib/types";

// Backend returns { models: [...], total_count, last_updated, tier }
interface ModelsListResponse {
  models: ModelInfo[];
  total_count: number;
  last_updated: string;
  tier?: PaidTier;
}

export const modelsApi = {
  // Get selected models
  getSelectedFreeModel: () => api.get<SelectedModel>("/model/free"),
  getSelectedPaidModel: () => api.get<SelectedModel>("/model/paid"),
  getSelectedImageModel: () => api.get<SelectedModel>("/model/image"),

  /** Selected model for one paid tier (low | moderate | premium). */
  getSelectedPaidModelForTier: (tier: PaidTier) =>
    api.get<SelectedModel>(`/model/paid/${tier}`),

  // Get all models - extract models array from response
  getFreeModels: async () => {
    const response = await api.get<ModelsListResponse>("/models/free");
    return response.models || [];
  },
  getPaidModels: async () => {
    const response = await api.get<ModelsListResponse>("/models/paid");
    return response.models || [];
  },
  getImageModels: async () => {
    const response = await api.get<ModelsListResponse>("/models/image");
    return response.models || [];
  },

  /**
   * Models in one paid tier, ordered the way the chat fallback chain walks them:
   * curated models first, then the rest of the tier's price band cheapest first.
   */
  getPaidModelsForTier: async (tier: PaidTier) => {
    const response = await api.get<ModelsListResponse>(`/models/paid/${tier}`);
    return response.models || [];
  },

  // Set selected models
  setSelectedFreeModel: (modelId: string) =>
    api.put("/admin/model/free", { model_id: modelId }),
  setSelectedPaidModel: (modelId: string) =>
    api.put("/admin/model/paid", { model_id: modelId }),
  setSelectedImageModel: (modelId: string) =>
    api.put("/admin/model/image", { model_id: modelId }),

  /** Sets the selected model for one paid tier. The model must belong to that tier. */
  setSelectedPaidModelForTier: (tier: PaidTier, modelId: string) =>
    api.put(`/admin/model/paid/${tier}`, { model_id: modelId }),
};
