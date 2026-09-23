"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { CircleAlert, Eye, EyeOff } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import { Mark } from "@/components/brand/mark";
import { authApi } from "@/lib/api/auth";
import { useAuthStore } from "@/lib/stores/auth-store";
import { LANES, LANE_ORDER } from "@/lib/theme/lanes";
import { cn } from "@/lib/utils/cn";

export default function LoginPage() {
  const router = useRouter();
  const setAuth = useAuthStore((s) => s.setAuth);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [reveal, setReveal] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const r = await authApi.login({ email, password });
      setAuth(r.token, r.user, r.expires_at);
      router.push("/");
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "That email and password did not match."
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="flex items-center justify-center px-6 py-14 sm:px-12">
        <div className="w-full max-w-sm">
          <span className="block h-9 w-9">
            <Mark />
          </span>

          <h1 className="mt-8 text-4xl font-semibold tracking-tight text-text">Freeway</h1>
          <p className="mt-2 text-sm text-text-2">
            Telemetry and routing for your gateway.
          </p>

          <form onSubmit={submit} className="mt-9 space-y-4" noValidate>
            <Field
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@example.com"
              autoComplete="email"
              required
              autoFocus
            />

            <div className="relative">
              <Field
                label="Password"
                type={reveal ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                autoComplete="current-password"
                required
                className="pr-10"
              />
              <button
                type="button"
                onClick={() => setReveal(!reveal)}
                aria-label={reveal ? "Hide password" : "Show password"}
                className="absolute right-1.5 top-[26px] grid h-8 w-8 place-items-center rounded text-text-3 transition-colors hover:text-text"
              >
                {reveal ? <EyeOff className="h-4 w-4" aria-hidden /> : <Eye className="h-4 w-4" aria-hidden />}
              </button>
            </div>

            {error && (
              <div
                role="alert"
                className="flex items-start gap-2.5 rounded border border-bad/30 bg-bad/[0.07] px-3.5 py-2.5"
              >
                <CircleAlert className="mt-px h-4 w-4 shrink-0 text-bad" aria-hidden />
                <p className="text-sm text-bad">{error}</p>
              </div>
            )}

            <Button type="submit" variant="solid" size="lg" className="w-full" busy={busy}>
              Sign in
            </Button>
          </form>
        </div>
      </div>

      {/* The routing table itself, told with the product's own vocabulary. */}
      <aside className="relative hidden items-center overflow-hidden border-l border-hair bg-panel px-12 lg:flex">
        <div className="ticks absolute inset-x-0 top-0 h-px opacity-50" aria-hidden />
        <div className="w-full max-w-md">
          <p className="text-2xs font-medium uppercase text-text-3">The routing table</p>
          <h2 className="mt-3 text-2xl font-semibold tracking-tight text-text">
            Ask for a lane, not a model
          </h2>
          <p className="mt-2 text-sm leading-relaxed text-text-2">
            Freeway picks the provider, steps around whichever one is throttling, and records what
            every request actually cost.
          </p>

          <ul className="enter mt-8 space-y-px overflow-hidden rounded-panel border border-hair">
            {LANE_ORDER.map((key, i) => {
              const lane = LANES[key];
              return (
                <li
                  key={key}
                  className={cn(
                    lane.scope,
                    "flex items-center gap-3 bg-raised px-4 py-3",
                    i > 0 && "border-t border-hair"
                  )}
                >
                  <span className="grid h-7 w-7 shrink-0 place-items-center rounded lane-soft lane-fg">
                    <lane.icon className="h-3.5 w-3.5" aria-hidden />
                  </span>
                  <code className="font-mono text-sm text-text">{lane.model}</code>
                  <span
                    className="ml-auto h-1 rounded-full lane-bg"
                    style={{ width: `${96 - i * 16}px` }}
                    aria-hidden
                  />
                </li>
              );
            })}
          </ul>

          <p className="mt-5 text-xs text-text-3">
            Lane colour tracks cost, from free through to premium.
          </p>
        </div>
      </aside>
    </div>
  );
}
