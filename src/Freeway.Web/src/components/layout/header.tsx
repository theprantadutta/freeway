"use client";

import { ReactNode } from "react";
import { Sun, Moon, Monitor } from "lucide-react";
import { useThemeStore } from "@/lib/stores/theme-store";
import { LaneMark } from "@/components/brand/lane-mark";
import { cn } from "@/lib/utils/cn";

interface HeaderProps {
  title: string;
  subtitle?: string;
  /** Page-level controls, right aligned next to the theme switch. */
  actions?: ReactNode;
}

export function Header({ title, subtitle, actions }: HeaderProps) {
  return (
    <header className="sticky top-0 z-30 border-b border-line bg-surface/85 backdrop-blur-md">
      <div className="flex min-h-14 flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-2.5 md:px-6">
        <div className="flex min-w-0 items-center gap-2.5">
          <span className="h-6 w-6 shrink-0 md:hidden">
            <LaneMark />
          </span>
          <div className="min-w-0">
            <h1 className="truncate text-lg font-semibold tracking-tight text-ink">
              {title}
            </h1>
            {subtitle && (
              <p className="truncate text-sm text-muted">{subtitle}</p>
            )}
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {actions}
          <ThemeSwitch />
        </div>
      </div>
    </header>
  );
}

const THEMES = [
  { value: "light", label: "Light", icon: Sun },
  { value: "dark", label: "Dark", icon: Moon },
  { value: "system", label: "System", icon: Monitor },
] as const;

/** Three explicit choices beat a button that cycles through hidden states. */
function ThemeSwitch() {
  const { theme, setTheme } = useThemeStore();

  return (
    <div
      role="radiogroup"
      aria-label="Colour theme"
      className="flex items-center gap-0.5 rounded-control bg-inset p-0.5"
    >
      {THEMES.map((option) => {
        const isActive = theme === option.value;
        return (
          <button
            key={option.value}
            role="radio"
            aria-checked={isActive}
            aria-label={option.label}
            title={option.label}
            onClick={() => setTheme(option.value)}
            className={cn(
              "grid h-7 w-7 place-items-center rounded-[0.3125rem] transition-colors duration-150 ease-swift",
              isActive
                ? "bg-panel text-brand shadow-pop"
                : "text-subtle hover:text-ink"
            )}
          >
            <option.icon className="h-3.5 w-3.5" aria-hidden />
          </button>
        );
      })}
    </div>
  );
}
