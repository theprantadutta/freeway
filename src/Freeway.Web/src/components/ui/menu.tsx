"use client";

import {
  ReactNode,
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import { createPortal } from "react-dom";
import { cn } from "@/lib/utils/cn";

/**
 * A dropdown that is actually visible.
 *
 * Two things a plain absolutely-positioned div gets wrong here, both of which this
 * exists to avoid:
 *
 * 1. It is clipped. Panels round their corners with `overflow-hidden`, so a menu
 *    opened from a row inside one is cut off at the panel's edge — worst on the last
 *    row, where almost nothing survives. So the surface renders in a portal at the
 *    document root, positioned against the trigger's viewport rect.
 *
 * 2. It has no elevation. Rows go to `--raised` on hover and the light theme paints
 *    `--panel` and `--raised` the same white, so a menu filled with `--raised` is the
 *    same colour as whatever it opens over: a pane of glass with a hairline round it.
 *    The `--overlay` surface plus a real shadow is what makes it read as floating.
 *
 * It flips above the trigger when there is no room below, and follows the trigger on
 * scroll and resize rather than detaching from it.
 */

const GAP = 6;
const MARGIN = 8;

interface MenuProps {
  /** Rendered as the trigger; receives the props that make it a real menu button. */
  trigger: (props: {
    ref: React.Ref<HTMLButtonElement>;
    onClick: () => void;
    "aria-expanded": boolean;
    "aria-haspopup": "menu";
  }) => ReactNode;
  /** Menu contents. `close` is passed so items can dismiss the menu when they run. */
  children: (close: () => void) => ReactNode;
  /** Which edge of the trigger the menu lines up with. */
  align?: "left" | "right";
  className?: string;
}

export function Menu({ trigger, children, align = "right", className }: MenuProps) {
  const [open, setOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [pos, setPos] = useState<{ top: number; left: number; up: boolean } | null>(null);

  const buttonRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  const close = useCallback(() => setOpen(false), []);

  // Portals need a document, which the server render does not have.
  useEffect(() => setMounted(true), []);

  const place = useCallback(() => {
    const button = buttonRef.current;
    const panel = panelRef.current;
    if (!button || !panel) return;

    const t = button.getBoundingClientRect();
    const p = panel.getBoundingClientRect();

    const below = window.innerHeight - t.bottom;
    const up = below < p.height + GAP + MARGIN && t.top > below;

    const left =
      align === "right"
        ? Math.min(t.right - p.width, window.innerWidth - p.width - MARGIN)
        : Math.max(t.left, MARGIN);

    setPos({
      top: up ? t.top - p.height - GAP : t.bottom + GAP,
      left: Math.max(MARGIN, left),
      up,
    });
  }, [align]);

  // Measured after paint, so the panel has a height to position against.
  useLayoutEffect(() => {
    if (!open) {
      setPos(null);
      return;
    }
    place();
  }, [open, place]);

  useEffect(() => {
    if (!open) return;

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    const onPointer = (e: PointerEvent) => {
      const target = e.target as Node;
      if (panelRef.current?.contains(target) || buttonRef.current?.contains(target)) return;
      setOpen(false);
    };

    document.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onPointer);
    // `true` so this also follows scrolling containers, not just the window.
    window.addEventListener("scroll", place, true);
    window.addEventListener("resize", place);
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", onPointer);
      window.removeEventListener("scroll", place, true);
      window.removeEventListener("resize", place);
    };
  }, [open, place]);

  return (
    <>
      {trigger({
        ref: buttonRef,
        onClick: () => setOpen((v) => !v),
        "aria-expanded": open,
        "aria-haspopup": "menu",
      })}

      {mounted &&
        open &&
        createPortal(
          <div
            ref={panelRef}
            role="menu"
            style={{
              top: pos?.top ?? 0,
              left: pos?.left ?? 0,
              // Invisible for the single frame before it has been measured, so it
              // never flashes in the top-left corner.
              visibility: pos ? "visible" : "hidden",
            }}
            className={cn(
              "fixed z-50 w-52 overflow-hidden rounded border border-hair-bright",
              "bg-overlay py-1 shadow-overlay animate-pop",
              className
            )}
          >
            {children(close)}
          </div>,
          document.body
        )}
    </>
  );
}

export function MenuItem({
  icon: Icon,
  onClick,
  tone,
  children,
}: {
  icon?: React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;
  onClick: () => void;
  tone?: "danger";
  children: ReactNode;
}) {
  return (
    <button
      role="menuitem"
      onClick={onClick}
      className={cn(
        "flex w-full items-center gap-2.5 px-3 py-2 text-left text-sm transition-colors",
        tone === "danger"
          ? "text-bad hover:bg-bad/10"
          : "text-text-2 hover:bg-raised hover:text-text"
      )}
    >
      {Icon && <Icon className="h-3.5 w-3.5" aria-hidden />}
      {children}
    </button>
  );
}
