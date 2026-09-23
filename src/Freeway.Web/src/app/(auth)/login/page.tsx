"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Eye, EyeOff, CircleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { LaneMark } from "@/components/brand/lane-mark";
import { authApi } from "@/lib/api/auth";
import { useAuthStore } from "@/lib/stores/auth-store";
import { ACCENTS, type AccentKey } from "@/lib/theme/accents";
import { cn } from "@/lib/utils/cn";

export default function LoginPage() {
  const router = useRouter();
  const setAuth = useAuthStore((state) => state.setAuth);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setIsLoading(true);

    try {
      const response = await authApi.login({ email, password });
      setAuth(response.token, response.user, response.expires_at);
      router.push("/");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "That email and password did not match. Try again."
      );
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-[1fr_1.1fr]">
      {/* Form */}
      <div className="flex items-center justify-center px-6 py-12 sm:px-10">
        <div className="w-full max-w-sm">
          <div className="flex items-center gap-2.5">
            <span className="h-8 w-8">
              <LaneMark />
            </span>
            <span className="text-xl font-semibold tracking-tight text-ink">
              Freeway
            </span>
          </div>

          <h1 className="mt-8 text-3xl font-semibold tracking-tight text-ink">
            Sign in
          </h1>
          <p className="mt-1.5 text-sm text-muted">
            Your gateway&rsquo;s control panel: routing, keys and spend.
          </p>

          <form onSubmit={handleSubmit} className="mt-8 space-y-4" noValidate>
            <Input
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@example.com"
              required
              autoComplete="email"
              autoFocus
            />

            <div className="relative">
              <Input
                label="Password"
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                required
                autoComplete="current-password"
                className="pr-10"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                aria-label={showPassword ? "Hide password" : "Show password"}
                className="absolute right-2 top-[30px] grid h-8 w-8 place-items-center rounded-control text-subtle transition-colors hover:text-ink"
              >
                {showPassword ? (
                  <EyeOff className="h-4 w-4" aria-hidden />
                ) : (
                  <Eye className="h-4 w-4" aria-hidden />
                )}
              </button>
            </div>

            {error && (
              <div
                role="alert"
                className="flex items-start gap-2.5 rounded-control border border-danger/30 bg-danger/[0.06] px-3.5 py-2.5"
              >
                <CircleAlert className="mt-px h-4 w-4 shrink-0 text-danger" aria-hidden />
                <p className="text-sm text-danger">{error}</p>
              </div>
            )}

            <Button type="submit" size="lg" className="w-full" isLoading={isLoading}>
              Sign in
            </Button>
          </form>
        </div>
      </div>

      {/* What this thing does, told with the product's own routing table. */}
      <aside className="relative hidden items-center overflow-hidden border-l border-line bg-panel px-12 lg:flex">
        <div className="relative w-full max-w-md">
          <h2 className="text-2xl font-semibold tracking-tight text-ink">
            One endpoint, five lanes
          </h2>
          <p className="mt-2 text-sm leading-relaxed text-muted">
            Ask for a lane instead of a model. Freeway picks the provider, falls back
            when one throttles, and records what every request cost.
          </p>

          <ul className="stagger mt-8 space-y-2.5">
            {(["free", "low", "moderate", "premium", "image"] as AccentKey[]).map(
              (key, i) => {
                const def = ACCENTS[key];
                const Icon = def.icon;
                return (
                  <li
                    key={key}
                    className={cn(
                      def.scope,
                      "flex items-center gap-3 rounded-panel border border-line bg-surface px-4 py-3"
                    )}
                  >
                    <span className="grid h-8 w-8 shrink-0 place-items-center rounded-control accent-tint-strong accent-text">
                      <Icon className="h-4 w-4" aria-hidden />
                    </span>
                    <code className="font-mono text-sm text-ink">{def.model}</code>
                    <span
                      className="ml-auto h-1.5 rounded-full accent-bg"
                      style={{ width: `${88 - i * 14}px` }}
                      aria-hidden
                    />
                  </li>
                );
              }
            )}
          </ul>

          <p className="mt-6 text-xs text-subtle">
            Lane colour tracks cost, from free through to premium.
          </p>
        </div>
      </aside>
    </div>
  );
}
