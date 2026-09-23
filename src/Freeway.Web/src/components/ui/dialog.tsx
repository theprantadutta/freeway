"use client";

import { ReactNode, useEffect, useCallback, useRef } from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/utils/cn";

interface DialogProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
}

export function Dialog({
  isOpen,
  onClose,
  title,
  description,
  children,
  className,
}: DialogProps) {
  const panelRef = useRef<HTMLDivElement>(null);

  const handleEscape = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    },
    [onClose]
  );

  useEffect(() => {
    if (!isOpen) return;
    document.addEventListener("keydown", handleEscape);
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    // Move focus into the dialog so keyboard users are not left behind it.
    panelRef.current?.focus();
    return () => {
      document.removeEventListener("keydown", handleEscape);
      document.body.style.overflow = previous;
    };
  }, [isOpen, handleEscape]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-0 sm:p-4">
      <div
        className="absolute inset-0 bg-ink/40 backdrop-blur-[2px] animate-scale-in"
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
          "relative z-10 w-full sm:max-w-md bg-panel border border-line shadow-modal outline-none",
          "rounded-t-modal sm:rounded-modal animate-slide-over",
          className
        )}
      >
        <div className="flex items-start justify-between gap-4 px-5 py-4 border-b border-line">
          <div className="min-w-0">
            <h2 className="text-base font-semibold text-ink">{title}</h2>
            {description && (
              <p className="mt-0.5 text-sm text-muted">{description}</p>
            )}
          </div>
          <button
            onClick={onClose}
            aria-label="Close"
            className="-mr-1 -mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-control text-subtle transition-colors hover:bg-inset hover:text-ink"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

export function DialogActions({ children }: { children: ReactNode }) {
  return (
    <div className="mt-6 flex items-center justify-end gap-2 border-t border-line pt-4 -mx-5 px-5 -mb-5 pb-5">
      {children}
    </div>
  );
}
