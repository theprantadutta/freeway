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
      setError(err instanceof Error ? err.message : "That email and password did not match.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-[minmax(0,1fr)_minmax(0,1.15fr)]">
      <div className="relative flex items-center justify-center px-6 py-14 sm:px-12">
        <div className="reveal w-full max-w-md">
          <div className="flex items-center gap-3">
            <span className="block h-11 w-11">
              <Mark />
            </span>
            <span className="text-2xl font-extrabold tracking-tight text-text">Freeway</span>
          </div>

          <h1 className="mt-12 text-4xl font-extrabold leading-tight">
            <span className="text-grad">Welcome back.</span>
          </h1>
          <p className="mt-3 text-lg text-text-2">Sign in to see where your requests went and what they cost.</p>

          <form onSubmit={submit} className="card mt-10 space-y-5 p-7" noValidate>
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
                className="pr-12"
              />
              <button
                type="button"
                onClick={() => setReveal(!reveal)}
                aria-label={reveal ? "Hide password" : "Show password"}
                className="absolute right-1.5 top-[30px] grid h-9 w-9 place-items-center rounded-lg text-text-3 transition-colors hover:text-text"
              >
                {reveal ? <EyeOff className="h-5 w-5" aria-hidden /> : <Eye className="h-5 w-5" aria-hidden />}
              </button>
            </div>

            {error && (
              <div role="alert" className="flex items-start gap-3 rounded-xl border border-bad/35 bg-bad/[0.08] px-4 py-3 animate-lift">
                <CircleAlert className="mt-0.5 h-5 w-5 shrink-0 text-bad" aria-hidden />
                <p className="text-base text-text">{error}</p>
              </div>
            )}

            <Button type="submit" variant="solid" size="lg" className="w-full" busy={busy}>
              Sign in
            </Button>
          </form>

          <p className="mt-6 text-sm text-text-3">Only the gateway&apos;s administrators can sign in here.</p>
        </div>
      </div>

      <aside className="relative hidden overflow-hidden border-l border-hair/60 lg:block">
        <Highway />
        <div className="relative z-10 flex h-full flex-col justify-end p-14">
          <div className="reveal max-w-lg" style={{ ["--d" as string]: "200ms" }}>
            <h2 className="text-4xl font-extrabold leading-tight text-white">Every request takes the cheapest road that works.</h2>
            <p className="mt-4 text-lg text-white/75">
              Apps ask for a lane. Freeway sends each request down whatever is already paid for first, and pays only
              when nothing else can answer.
            </p>
            <ul className="mt-8 flex flex-wrap gap-2.5">
              {LANE_ORDER.map((key) => {
                const lane = LANES[key];
                return (
                  <li
                    key={key}
                    className={cn(
                      lane.scope,
                      "flex items-center gap-2 rounded-full bg-white/10 px-3.5 py-1.5 text-sm font-semibold text-white ring-1 ring-white/15 backdrop-blur"
                    )}
                  >
                    <span className="h-2.5 w-2.5 rounded-full lane-bg" aria-hidden />
                    {lane.label}
                  </li>
                );
              })}
            </ul>
          </div>
        </div>
      </aside>
    </div>
  );
}

/**
 * Five lanes running toward a dusk horizon, their markings moving toward the
 * viewer. The one piece of pure atmosphere in the console, on the one page where
 * there is nothing to read yet; stilled for reduced motion.
 */
function Highway() {
  const lanes = LANE_ORDER.map((k) => LANES[k]);
  const W = 1000;
  const H = 1000;
  const horizon = 380;
  const vanishX = W / 2;
  const spread = 1500;

  return (
    <div className="absolute inset-0" aria-hidden>
      <div
        className="absolute inset-0"
        style={{
          background:
            "radial-gradient(60% 40% at 50% 36%, rgb(251 146 60 / 0.75), transparent 70%), radial-gradient(80% 50% at 20% 20%, rgb(217 70 239 / 0.55), transparent 70%), radial-gradient(70% 50% at 85% 25%, rgb(34 211 238 / 0.35), transparent 70%), linear-gradient(180deg, #1b0b3d 0%, #2a0f4f 38%, #120826 39%, #0b0620 100%)",
        }}
      />
      <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="xMidYMid slice" className="absolute inset-0 h-full w-full">
        <defs>
          <linearGradient id="road" x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" stopColor="#1c0f3f" stopOpacity="0.4" />
            <stop offset="100%" stopColor="#07041a" stopOpacity="0.95" />
          </linearGradient>
          <style>{`@keyframes dash-in{to{stroke-dashoffset:-140}}`}</style>
        </defs>
        <polygon
          points={`${vanishX - 6},${horizon} ${vanishX + 6},${horizon} ${vanishX + spread / 2},${H} ${vanishX - spread / 2},${H}`}
          fill="url(#road)"
        />
        {lanes.map((lane, i) => {
          const t = (i + 0.5) / lanes.length - 0.5;
          const x1 = vanishX + t * 10;
          const x2 = vanishX + t * spread * 0.92;
          return (
            <g key={lane.key} className={lane.scope}>
              <line x1={x1} y1={horizon} x2={x2} y2={H} stroke="rgb(var(--lane))" strokeOpacity={0.3} strokeWidth={12} />
              <line
                x1={x1}
                y1={horizon}
                x2={x2}
                y2={H}
                stroke="rgb(var(--lane))"
                strokeWidth={6}
                strokeLinecap="round"
                strokeDasharray="40 100"
                style={{ animation: `dash-in ${2.4 + i * 0.25}s linear infinite` }}
              />
            </g>
          );
        })}
        <line x1={0} y1={horizon} x2={W} y2={horizon} stroke="rgb(255 210 160)" strokeOpacity={0.5} strokeWidth={1.5} />
      </svg>
      <div className="absolute inset-0 bg-gradient-to-t from-[#0b0620] via-[#0b0620]/40 to-transparent" />
    </div>
  );
}
