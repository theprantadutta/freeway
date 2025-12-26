import { api } from "./client";
import type { LoginRequest, LoginResponse, User } from "@/lib/types";

export const authApi = {
  login: (data: LoginRequest) =>
    api.post<LoginResponse>("/auth/login", data, { skipAuth: true }),

  me: () => api.get<User>("/auth/me"),

  logout: () => api.post("/auth/logout"),

  changePassword: (currentPassword: string, newPassword: string) =>
    api.post("/auth/change-password", {
      current_password: currentPassword,
      new_password: newPassword,
    }),
};
