import type { Config } from "tailwindcss";

/**
 * Freeway console tokens.
 *
 * Night-highway palette: a midnight surface with the lanes lit in their own
 * colours. Structure is ink on navy; hue means one thing only, which lane a request
 * took. See globals.css for the values and the reasons behind them.
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
        // The road surface. Named `page`, never `base`: a colour called `base` makes
        // `text-base` set the text colour as well as the size, painting body text in
        // the background colour wherever nothing else overrides it.
        page: "rgb(var(--base) / <alpha-value>)",
        // Text sitting on a solid ink fill (a primary button).
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
        sans: ["var(--font-sans)", "system-ui", "sans-serif"],
        mono: ["var(--font-mono)", "ui-monospace", "monospace"],
      },
      fontSize: {
        // 12 / 13 / 14 / 15 / 17 / 22 / 28 / 36 / 46 / 60. Body sits at 15px: this is a
        // screen people read from across a desk, and the previous 13px body made every
        // page squint. 12px is for axis ticks and nothing else.
        "2xs": ["0.75rem", { lineHeight: "1rem", letterSpacing: "0.01em" }],
        xs: ["0.8125rem", { lineHeight: "1.125rem", letterSpacing: "0.005em" }],
        sm: ["0.875rem", { lineHeight: "1.3125rem" }],
        base: ["0.9375rem", { lineHeight: "1.5rem" }],
        lg: ["1.0625rem", { lineHeight: "1.5rem", letterSpacing: "-0.005em" }],
        xl: ["1.375rem", { lineHeight: "1.75rem", letterSpacing: "-0.012em" }],
        "2xl": ["1.75rem", { lineHeight: "2.125rem", letterSpacing: "-0.018em" }],
        "3xl": ["2.25rem", { lineHeight: "2.5rem", letterSpacing: "-0.024em" }],
        "4xl": ["2.875rem", { lineHeight: "3.125rem", letterSpacing: "-0.03em" }],
        "5xl": ["3.75rem", { lineHeight: "1", letterSpacing: "-0.035em" }],
      },
      spacing: {
        18: "4.5rem",
        30: "7.5rem",
      },
      borderRadius: {
        DEFAULT: "0.5rem",
        panel: "1rem",
      },
      transitionTimingFunction: {
        out: "cubic-bezier(0.16, 1, 0.3, 1)",
        spring: "cubic-bezier(0.34, 1.56, 0.64, 1)",
      },
      boxShadow: {
        overlay: "var(--overlay-shadow)",
        card: "var(--card-shadow)",
      },
      keyframes: {
        lift: {
          from: { opacity: "0", transform: "translateY(10px)" },
          to: { opacity: "1", transform: "none" },
        },
        pop: {
          from: { opacity: "0", transform: "scale(0.96)" },
          to: { opacity: "1", transform: "none" },
        },
        fade: {
          from: { opacity: "0" },
          to: { opacity: "1" },
        },
      },
      animation: {
        lift: "lift 520ms cubic-bezier(0.16,1,0.3,1) both",
        pop: "pop 200ms cubic-bezier(0.16,1,0.3,1) both",
        fade: "fade 400ms ease-out both",
      },
    },
  },
  plugins: [],
} satisfies Config;
