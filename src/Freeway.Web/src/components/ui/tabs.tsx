"use client";

import { createContext, useContext, useState, ReactNode } from "react";
import { cn } from "@/lib/utils/cn";

interface TabsContextValue {
  activeTab: string;
  setActiveTab: (tab: string) => void;
}

const TabsContext = createContext<TabsContextValue | undefined>(undefined);

interface TabsProps {
  defaultValue: string;
  children: ReactNode;
  className?: string;
  onChange?: (value: string) => void;
  /** Controlled value. When set, the tab list follows it. */
  value?: string;
}

export function Tabs({ defaultValue, children, className, onChange, value }: TabsProps) {
  const [internal, setInternal] = useState(defaultValue);
  const activeTab = value ?? internal;

  const handleTabChange = (tab: string) => {
    setInternal(tab);
    onChange?.(tab);
  };

  return (
    <TabsContext.Provider value={{ activeTab, setActiveTab: handleTabChange }}>
      <div className={cn("w-full", className)}>{children}</div>
    </TabsContext.Provider>
  );
}

export function TabsList({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <div
      role="tablist"
      className={cn(
        "inline-flex items-center gap-0.5 rounded-control bg-inset p-0.5",
        className
      )}
    >
      {children}
    </div>
  );
}

interface TabsTriggerProps {
  value: string;
  children: ReactNode;
  className?: string;
  /** Accent scope applied when this tab is active. */
  accent?: string;
}

export function TabsTrigger({ value, children, className, accent }: TabsTriggerProps) {
  const context = useContext(TabsContext);
  if (!context) throw new Error("TabsTrigger must be used within Tabs");

  const isActive = context.activeTab === value;

  return (
    <button
      role="tab"
      aria-selected={isActive}
      onClick={() => context.setActiveTab(value)}
      className={cn(
        "relative flex-1 whitespace-nowrap rounded-[0.3125rem] px-3 py-1.5 text-sm font-medium",
        "transition-[background-color,color] duration-150 ease-swift",
        isActive
          ? cn("bg-panel shadow-pop", accent ? `${accent} accent-text` : "text-ink")
          : "text-muted hover:text-ink",
        className
      )}
    >
      {children}
    </button>
  );
}

export function TabsContent({
  value,
  children,
  className,
}: {
  value: string;
  children: ReactNode;
  className?: string;
}) {
  const context = useContext(TabsContext);
  if (!context) throw new Error("TabsContent must be used within Tabs");
  if (context.activeTab !== value) return null;
  return <div className={cn("mt-4 animate-rise-in", className)}>{children}</div>;
}
