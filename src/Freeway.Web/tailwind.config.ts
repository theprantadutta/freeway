import type { Config } from "tailwindcss";

/**
 * Freeway design tokens.
 *
 * Rule of the system: every model type owns a hue and keeps it everywhere it
 * appears. Structure stays neutral so the colour reads as information rather
 * than decoration. Large areas take the hue at low alpha; saturated colour is
 * reserved for text, icons, rails and chart marks.
 */
export default {
  // Accent scope classes live in src/lib/theme/accents.ts, so the whole of src
  // must be scanned or those classes get purged.
  content: ["./src/**/*.{js,ts,jsx,tsx,mdx}"],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        // Structure
        surface: "rgb(var(--surface) / <alpha-value>)",
        panel: "rgb(var(--panel) / <alpha-value>)",
        inset: "rgb(var(--inset) / <alpha-value>)",
        line: "rgb(var(--line) / <alpha-value>)",
        "line-strong": "rgb(var(--line-strong) / <alpha-value>)",

        // Text
        ink: "rgb(var(--ink) / <alpha-value>)",
        muted: "rgb(var(--muted) / <alpha-value>)",
        subtle: "rgb(var(--subtle) / <alpha-value>)",

        // Actions
        brand: "rgb(var(--brand) / <alpha-value>)",
        focus: "rgb(var(--focus) / <alpha-value>)",

        // One hue per model type. These follow a type everywhere it appears.
        free: "rgb(var(--free) / <alpha-value>)",
        low: "rgb(var(--low) / <alpha-value>)",
        moderate: "rgb(var(--moderate) / <alpha-value>)",
        premium: "rgb(var(--premium) / <alpha-value>)",
        image: "rgb(var(--image) / <alpha-value>)",

        // Whatever the nearest .accent-* scope resolves to.
        accent: "rgb(var(--accent) / <alpha-value>)",

        // Status
        ok: "rgb(var(--ok) / <alpha-value>)",
        warn: "rgb(var(--warn) / <alpha-value>)",
        danger: "rgb(var(--danger) / <alpha-value>)",
        info: "rgb(var(--info) / <alpha-value>)",

        // Chart marks. Same values in both modes - see globals.css.
        mark: {
          DEFAULT: "rgb(var(--mark) / <alpha-value>)",
          free: "rgb(var(--mark-free) / <alpha-value>)",
          low: "rgb(var(--mark-low) / <alpha-value>)",
          moderate: "rgb(var(--mark-moderate) / <alpha-value>)",
          premium: "rgb(var(--mark-premium) / <alpha-value>)",
          image: "rgb(var(--mark-image) / <alpha-value>)",
          brand: "rgb(var(--mark-brand) / <alpha-value>)",
        },

        // Retained so any un-migrated utility keeps resolving.
        primary: {
          50: "#eef2ff",
          100: "#e0e7ff",
          200: "#c7d2fe",
          300: "#a5b4fc",
          400: "#818cf8",
          500: "#6366f1",
          600: "#4f46e5",
          700: "#4338ca",
          800: "#3730a3",
          900: "#312e81",
          950: "#1e1b4b",
        },
      },
      fontFamily: {
        sans: ["var(--font-plex-sans)", "system-ui", "sans-serif"],
        mono: ["var(--font-plex-mono)", "ui-monospace", "monospace"],
      },
      fontSize: {
        // Tightened scale. Display sizes get negative tracking; small text gets none.
        micro: ["0.6875rem", { lineHeight: "1rem", letterSpacing: "0.005em" }],
        xs: ["0.75rem", { lineHeight: "1.125rem" }],
        sm: ["0.8125rem", { lineHeight: "1.25rem" }],
        base: ["0.875rem", { lineHeight: "1.375rem" }],
        lg: ["1rem", { lineHeight: "1.5rem", letterSpacing: "-0.006em" }],
        xl: ["1.125rem", { lineHeight: "1.625rem", letterSpacing: "-0.011em" }],
        "2xl": ["1.375rem", { lineHeight: "1.75rem", letterSpacing: "-0.014em" }],
        "3xl": ["1.75rem", { lineHeight: "2.125rem", letterSpacing: "-0.019em" }],
        "4xl": ["2.25rem", { lineHeight: "2.5rem", letterSpacing: "-0.022em" }],
      },
      borderRadius: {
        // Radius encodes role: controls are tighter than containers.
        control: "0.375rem",
        panel: "0.625rem",
        modal: "0.875rem",
      },
      boxShadow: {
        // Elevation is for things that genuinely float. Nothing else gets a shadow.
        pop: "0 1px 2px rgb(var(--shadow) / 0.06), 0 8px 24px -8px rgb(var(--shadow) / 0.18)",
        modal: "0 1px 2px rgb(var(--shadow) / 0.08), 0 24px 48px -12px rgb(var(--shadow) / 0.28)",
      },
      transitionTimingFunction: {
        swift: "cubic-bezier(0.32, 0.72, 0, 1)",
      },
      keyframes: {
        "rise-in": {
          from: { opacity: "0", transform: "translateY(4px)" },
          to: { opacity: "1", transform: "none" },
        },
        "scale-in": {
          from: { opacity: "0", transform: "scale(0.97)" },
          to: { opacity: "1", transform: "none" },
        },
        "slide-over": {
          from: { opacity: "0", transform: "translateY(8px)" },
          to: { opacity: "1", transform: "none" },
        },
        sweep: {
          from: { transform: "translateX(-100%)" },
          to: { transform: "translateX(100%)" },
        },
      },
      animation: {
        "rise-in": "rise-in 260ms cubic-bezier(0.32,0.72,0,1) both",
        "scale-in": "scale-in 160ms cubic-bezier(0.32,0.72,0,1) both",
        "slide-over": "slide-over 220ms cubic-bezier(0.32,0.72,0,1) both",
      },
    },
  },
  plugins: [],
} satisfies Config;
