import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { User } from "@/lib/types";

interface AuthState {
  token: string | null;
  user: User | null;
  expiresAt: string | null;
  isAuthenticated: boolean;
  _hasHydrated: boolean;
  setAuth: (token: string, user: User, expiresAt: string) => void;
  clearAuth: () => void;
  setHasHydrated: (state: boolean) => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      user: null,
      expiresAt: null,
      isAuthenticated: false,
      _hasHydrated: false,
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
      setHasHydrated: (state) => set({ _hasHydrated: state }),
    }),
    {
      name: "freeway-auth",
      onRehydrateStorage: () => (state) => {
        state?.setHasHydrated(true);
      },
    }
  )
);
