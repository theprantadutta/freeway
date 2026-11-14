"use client";

import { RefObject, useEffect, useLayoutEffect, useRef, useState } from "react";

/** True when the viewer has asked the system for less motion. */
export function useReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);
  useEffect(() => {
    const q = window.matchMedia("(prefers-reduced-motion: reduce)");
    setReduced(q.matches);
    const on = (e: MediaQueryListEvent) => setReduced(e.matches);
    q.addEventListener("change", on);
    return () => q.removeEventListener("change", on);
  }, []);
  return reduced;
}

/**
 * A number that travels to its new value instead of jumping. Counts up from zero the
 * first time, then eases between values when the data changes (a new time window, a
 * fresh poll), so a change reads as a change.
 */
export function useCountUp(target: number, duration = 900): number {
  const reduced = useReducedMotion();
  const [value, setValue] = useState(0);
  const from = useRef(0);
  const frame = useRef<number | null>(null);

  useEffect(() => {
    if (!Number.isFinite(target)) return;
    if (reduced) {
      setValue(target);
      from.current = target;
      return;
    }

    const start = performance.now();
    const origin = from.current;
    const tick = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
      // easeOutExpo: quick to arrive, gentle to settle.
      const eased = t === 1 ? 1 : 1 - Math.pow(2, -10 * t);
      const v = origin + (target - origin) * eased;
      setValue(v);
      if (t < 1) {
        frame.current = requestAnimationFrame(tick);
      } else {
        from.current = target;
      }
    };
    frame.current = requestAnimationFrame(tick);
    return () => {
      if (frame.current) cancelAnimationFrame(frame.current);
      from.current = target;
    };
  }, [target, duration, reduced]);

  return value;
}

/** The rendered width of an element, kept current as it resizes. */
export function useWidth<T extends HTMLElement>(): [RefObject<T | null>, number] {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(0);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    setWidth(el.getBoundingClientRect().width);
    const ro = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  return [ref, width];
}

/** Ids seen in an earlier render, so newly arrived rows can be marked. */
export function useArrivals(ids: string[]): Set<string> {
  const seen = useRef<Set<string> | null>(null);
  const [fresh, setFresh] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (seen.current === null) {
      // First render: nothing is new, everything was already there.
      seen.current = new Set(ids);
      return;
    }
    const arrived = ids.filter((id) => !seen.current!.has(id));
    ids.forEach((id) => seen.current!.add(id));
    if (arrived.length) setFresh(new Set(arrived));
  }, [ids.join("|")]); // eslint-disable-line react-hooks/exhaustive-deps

  return fresh;
}
