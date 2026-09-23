import type { Config } from "tailwindcss";

/**
 * Freeway console tokens.
 *
 * Dark-first: `.light` on <html> opts into the inverse, rather than dark being a
 * flip of a light design. Structure is achromatic; the only hue in the interface
 * tells you which lane a request took.
 *
 * Lane classes come from src/lib/theme/lanes.ts rather than appearing literally in
 * markup, so the content glob must cover all of ./src. Narrowing it silently purges
 * every lane colour.
 */
export default {
  content: ["./src/**/*.{js,ts,jsx,tsx,mdx}"],
  darkMode: ["class", ":root:not(.light)"],
  theme: {
    extend: {
      colors: {
        base: "rgb(var(--base) / <alpha-value>)",
        // Same value as `base`, under a name that cannot collide. `text-base` is
        // Tailwind's font-size utility, so it silently produces no colour at all;
        // use `text-inverse` for text sitting on a solid light fill.
        inverse: "rgb(var(--base) / <alpha-value>)",
        panel: "rgb(var(--panel) / <alpha-value>)",
        raised: "rgb(var(--raised) / <alpha-value>)",
        sunken: "rgb(var(--sunken) / <alpha-value>)",
        overlay: "rgb(var(--overlay) / <alpha-value>)",
        hair: "rgb(var(--hair) / <alpha-value>)",
        "hair-bright": "rgb(var(--hair-bright) / <alpha-value>)",

        text: "rgb(var(--text) / <alpha-value>)",
        "text-2": "rgb(var(--text-2) / <alpha-value>)",
        "text-3": "rgb(var(--text-3) / <alpha-value>)",

        accent: "rgb(var(--accent) / <alpha-value>)",
        lane: "rgb(var(--lane) / <alpha-value>)",

        free: "rgb(var(--free) / <alpha-value>)",
        low: "rgb(var(--low) / <alpha-value>)",
        moderate: "rgb(var(--moderate) / <alpha-value>)",
        premium: "rgb(var(--premium) / <alpha-value>)",
        image: "rgb(var(--image) / <alpha-value>)",

        ok: "rgb(var(--ok) / <alpha-value>)",
        warn: "rgb(var(--warn) / <alpha-value>)",
        bad: "rgb(var(--bad) / <alpha-value>)",
      },
      fontFamily: {
        sans: ["var(--font-display)", "system-ui", "sans-serif"],
        mono: ["var(--font-mono)", "ui-monospace", "monospace"],
      },
      fontSize: {
        // A scale with real distance in it. The previous one ran 11px to 30px and
        // read as a single size, which is why nothing on a page anchored the eye.
        "2xs": ["0.6875rem", { lineHeight: "0.875rem", letterSpacing: "0.04em" }],
        xs: ["0.75rem", { lineHeight: "1rem", letterSpacing: "0.01em" }],
        sm: ["0.8125rem", { lineHeight: "1.125rem" }],
        base: ["0.875rem", { lineHeight: "1.375rem" }],
        lg: ["1rem", { lineHeight: "1.5rem", letterSpacing: "-0.008em" }],
        xl: ["1.25rem", { lineHeight: "1.5rem", letterSpacing: "-0.018em" }],
        "2xl": ["1.625rem", { lineHeight: "1.75rem", letterSpacing: "-0.026em" }],
        "3xl": ["2.25rem", { lineHeight: "2.25rem", letterSpacing: "-0.032em" }],
        "4xl": ["3rem", { lineHeight: "1", letterSpacing: "-0.04em" }],
        "5xl": ["4rem", { lineHeight: "0.92", letterSpacing: "-0.045em" }],
      },
      spacing: {
        18: "4.5rem",
        30: "7.5rem",
      },
      borderRadius: {
        DEFAULT: "0.375rem",
        panel: "0.75rem",
      },
      transitionTimingFunction: {
        out: "cubic-bezier(0.16, 1, 0.3, 1)",
      },
      boxShadow: {
        // Theme-aware, because the light and dark themes separate an overlay from
        // the page in different ways. See --overlay-shadow in globals.css.
        overlay: "var(--overlay-shadow)",
      },
      keyframes: {
        lift: {
          from: { opacity: "0", transform: "translateY(8px)" },
          to: { opacity: "1", transform: "none" },
        },
        pop: {
          from: { opacity: "0", transform: "scale(0.96)" },
          to: { opacity: "1", transform: "none" },
        },
      },
      animation: {
        lift: "lift 400ms cubic-bezier(0.16,1,0.3,1) both",
        pop: "pop 180ms cubic-bezier(0.16,1,0.3,1) both",
      },
    },
  },
  plugins: [],
} satisfies Config;
