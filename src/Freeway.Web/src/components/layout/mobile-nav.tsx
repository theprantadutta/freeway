"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { LayoutDashboard, Brain, FolderKanban, Settings } from "lucide-react";
import { cn } from "@/lib/utils/cn";

const navItems = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard },
  { href: "/models", label: "Models", icon: Brain },
  { href: "/projects", label: "Projects", icon: FolderKanban },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function MobileNav() {
  const pathname = usePathname();

  return (
    <nav
      className="fixed inset-x-0 bottom-0 z-50 border-t border-line bg-panel/95 backdrop-blur-md md:hidden"
      style={{ paddingBottom: "env(safe-area-inset-bottom)" }}
    >
      <div className="flex items-stretch justify-around">
        {navItems.map((item) => {
          const isActive =
            pathname === item.href ||
            (item.href !== "/" && pathname.startsWith(item.href));

          return (
            <Link
              key={item.href}
              href={item.href}
              aria-current={isActive ? "page" : undefined}
              className={cn(
                "relative flex flex-1 flex-col items-center gap-1 py-2.5 transition-colors",
                isActive ? "text-brand" : "text-subtle"
              )}
            >
              {isActive && (
                <span
                  className="absolute inset-x-5 top-0 h-0.5 rounded-full bg-brand"
                  aria-hidden
                />
              )}
              <item.icon className="h-[18px] w-[18px]" aria-hidden />
              <span className="text-micro font-medium">{item.label}</span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
