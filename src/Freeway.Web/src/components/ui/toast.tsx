"use client";

import { ReactNode, createContext, useCallback, useContext, useState } from "react";
import { AlertTriangle, Check, Info, X, XCircle } from "lucide-react";
import { cn } from "@/lib/utils/cn";

type Tone = "success" | "error" | "info" | "warning";

interface Note {
  id: string;
  message: string;
  tone: Tone;
}

const Ctx = createContext<{ toast: (message: string, tone?: Tone) => void } | undefined>(
  undefined
);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [notes, setNotes] = useState<Note[]>([]);

  const toast = useCallback((message: string, tone: Tone = "info") => {
    const id = Math.random().toString(36).slice(2, 11);
    setNotes((n) => [...n, { id, message, tone }]);
    setTimeout(() => setNotes((n) => n.filter((x) => x.id !== id)), 5000);
  }, []);

  return (
    <Ctx.Provider value={{ toast }}>
      {children}
      <div
        className="pointer-events-none fixed inset-x-4 bottom-4 z-[60] flex flex-col items-center gap-2 sm:inset-x-auto sm:right-5 sm:items-end"
        role="region"
        aria-live="polite"
      >
        {notes.map((n) => (
          <Item key={n.id} note={n} onClose={() => setNotes((x) => x.filter((y) => y.id !== n.id))} />
        ))}
      </div>
    </Ctx.Provider>
  );
}

function Item({ note, onClose }: { note: Note; onClose: () => void }) {
  const icons = { success: Check, error: XCircle, info: Info, warning: AlertTriangle };
  const tones = {
    success: "text-ok",
    error: "text-bad",
    info: "text-accent",
    warning: "text-warn",
  };
  const Icon = icons[note.tone];

  return (
    <div className="pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded border border-hair-bright bg-raised px-3.5 py-3 animate-lift">
      <Icon className={cn("mt-px h-4 w-4 shrink-0", tones[note.tone])} aria-hidden />
      <p className="flex-1 text-sm text-text">{note.message}</p>
      <button
        onClick={onClose}
        aria-label="Dismiss"
        className="-mr-1 grid h-5 w-5 shrink-0 place-items-center rounded text-text-3 hover:text-text"
      >
        <X className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

export function useToast() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useToast must be used within ToastProvider");
  return ctx;
}
