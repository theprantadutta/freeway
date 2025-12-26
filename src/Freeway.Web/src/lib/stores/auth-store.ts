import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { User } from "@/lib/types";

interface AuthState {
  token: string | null;
  user: User | null;
  expiresAt: string | null;
  isAuthenticated: boolean;
  setAuth: (token: string, user: User, expiresAt: string) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      user: null,
      expiresAt: null,
      isAuthenticated: false,
      setAuth: (token, user, expiresAt) =>
        set({
          token,
          user,
          expiresAt,
          isAuthenticated: true,
        }),
      clearAuth: () =>
        set({
          token: null,
          user: null,
          expiresAt: null,
          isAuthenticated: false,
        }),
    }),
    {
      name: "freeway-auth",
    }
  )
);
