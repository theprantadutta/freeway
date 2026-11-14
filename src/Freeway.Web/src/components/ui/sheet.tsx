"use client";

import { ReactNode, useCallback, useEffect, useRef } from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/utils/cn";

/**
 * Replaces the old centred dialog. On a wide screen it slides in from the right so
 * the console stays visible behind it; on a phone it comes up from the bottom, where
 * a thumb can reach the actions.
 */
export function Sheet({
  open,
  onClose,
  title,
  description,
  children,
  className,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
}) {
  const panelRef = useRef<HTMLDivElement>(null);

  const onKey = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    },
    [onClose]
  );

  useEffect(() => {
    if (!open) return;
    document.addEventListener("keydown", onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    panelRef.current?.focus();
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = prev;
    };
  }, [open, onKey]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-stretch sm:justify-end">
      <div
        className="absolute inset-0 bg-[rgb(5_9_18/0.62)] backdrop-blur-[2px] animate-fade"
        onClick={onClose}
        aria-hidden
      />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        className={cn(
          "relative z-10 flex w-full flex-col bg-panel outline-none",
          "max-h-[92vh] rounded-t-panel border-t border-hair",
          "sm:h-full sm:max-h-none sm:w-[30rem] sm:rounded-none sm:border-l sm:border-t-0",
          "animate-lift",
          className
        )}
      >
        <div className="flex items-start justify-between gap-4 border-b border-hair px-6 py-5">
          <div className="min-w-0">
            <h2 className="text-xl font-bold text-text">{title}</h2>
            {description && <p className="mt-1 text-base text-text-2">{description}</p>}
          </div>
          <button
            onClick={onClose}
            aria-label="Close"
            className="-mr-1 grid h-9 w-9 shrink-0 place-items-center rounded-lg text-text-3 transition-colors hover:bg-raised hover:text-text"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto p-6">{children}</div>
      </div>
    </div>
  );
}

export function SheetActions({ children }: { children: ReactNode }) {
  return (
    <div className="-mx-6 -mb-6 mt-8 flex items-center justify-end gap-2 border-t border-hair bg-sunken px-6 py-4">
      {children}
    </div>
  );
}
