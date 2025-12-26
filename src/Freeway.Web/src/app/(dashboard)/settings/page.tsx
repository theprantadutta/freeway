"use client";

import { useAuthStore } from "@/lib/stores/auth-store";
import { useThemeStore } from "@/lib/stores/theme-store";
import { Header } from "@/components/layout/header";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  User,
  Link as LinkIcon,
  Key,
  Palette,
  Sun,
  Moon,
  Monitor,
  LogOut,
  Zap,
  Info,
  Shield,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils/cn";
import { formatDateTime } from "@/lib/utils/format";

export default function SettingsPage() {
  const user = useAuthStore((state) => state.user);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const { theme, setTheme } = useThemeStore();

  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

  const handleLogout = () => {
    clearAuth();
    window.location.href = "/login";
  };

  return (
    <div className="flex flex-col h-full">
      <Header title="Settings" subtitle="Manage your preferences" />

      <div className="flex-1 p-4 md:p-6 space-y-6">
        {/* User Info */}
        <section>
          <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
            Account
          </h3>
          <Card>
            <CardContent className="py-6">
              <div className="flex items-start gap-4">
                <div className="p-3 bg-primary-100 dark:bg-primary-900/30 rounded-lg">
                  <User className="h-6 w-6 text-primary-600 dark:text-primary-400" />
                </div>
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-medium text-gray-900 dark:text-gray-100">
                      {user?.name || user?.email}
                    </h3>
                    {user?.is_admin && (
                      <Badge variant="warning" className="text-xs">
                        <Shield className="h-3 w-3 mr-1" />
                        Admin
                      </Badge>
                    )}
                  </div>
                  <p className="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
                    {user?.email}
                  </p>
                  <div className="mt-3 text-sm text-gray-500 dark:text-gray-400">
                    <p>Member since: {user?.created_at ? formatDateTime(user.created_at) : "N/A"}</p>
                    {user?.last_login_at && (
                      <p>Last login: {formatDateTime(user.last_login_at)}</p>
                    )}
                  </div>
                </div>
                <Button variant="danger" size="sm" onClick={handleLogout}>
                  <LogOut className="h-4 w-4 mr-2" />
                  Logout
                </Button>
              </div>
            </CardContent>
          </Card>
        </section>

        {/* API Connection */}
        <section>
          <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
            API Connection
          </h3>
          <Card>
            <CardContent className="py-0 divide-y divide-gray-100 dark:divide-gray-800">
              <SettingsRow
                icon={LinkIcon}
                iconColor="text-blue-500"
                title="API Endpoint"
                value={apiUrl}
                mono
              />
              <SettingsRow
                icon={Key}
                iconColor="text-amber-500"
                title="Authentication"
                value="JWT Bearer Token"
              />
            </CardContent>
          </Card>
        </section>

        {/* Appearance */}
        <section>
          <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
            Appearance
          </h3>
          <Card>
            <CardContent className="py-6">
              <div className="flex items-center gap-4 mb-4">
                <div className="p-2.5 bg-purple-100 dark:bg-purple-900/30 rounded-lg">
                  <Palette className="h-5 w-5 text-purple-600 dark:text-purple-400" />
                </div>
                <div>
                  <h4 className="font-medium text-gray-900 dark:text-gray-100">
                    Theme Mode
                  </h4>
                  <p className="text-sm text-gray-500 dark:text-gray-400">
                    Choose your preferred theme
                  </p>
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <ThemeButton
                  icon={Sun}
                  label="Light"
                  isActive={theme === "light"}
                  onClick={() => setTheme("light")}
                />
                <ThemeButton
                  icon={Monitor}
                  label="System"
                  isActive={theme === "system"}
                  onClick={() => setTheme("system")}
                />
                <ThemeButton
                  icon={Moon}
                  label="Dark"
                  isActive={theme === "dark"}
                  onClick={() => setTheme("dark")}
                />
              </div>
            </CardContent>
          </Card>
        </section>

        {/* About */}
        <section>
          <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-4">
            About
          </h3>
          <Card>
            <CardContent className="py-0 divide-y divide-gray-100 dark:divide-gray-800">
              <SettingsRow
                icon={Zap}
                iconColor="text-primary-500"
                title="Freeway Control Panel"
                value="Version 1.0.0"
              />
              <SettingsRow
                icon={Info}
                iconColor="text-indigo-500"
                title="Built with"
                value="Next.js 15, Tailwind CSS"
              />
            </CardContent>
          </Card>
        </section>
      </div>
    </div>
  );
}

function SettingsRow({
  icon: Icon,
  iconColor,
  title,
  value,
  mono,
}: {
  icon: typeof User;
  iconColor: string;
  title: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="flex items-center gap-4 py-4">
      <div
        className={cn(
          "p-2 rounded-lg bg-gray-100 dark:bg-gray-800",
          iconColor
        )}
      >
        <Icon className="h-4 w-4" />
      </div>
      <div className="flex-1">
        <p className="text-sm font-medium text-gray-900 dark:text-gray-100">
          {title}
        </p>
        <p
          className={cn(
            "text-sm text-gray-500 dark:text-gray-400 mt-0.5",
            mono && "font-mono"
          )}
        >
          {value}
        </p>
      </div>
    </div>
  );
}

function ThemeButton({
  icon: Icon,
  label,
  isActive,
  onClick,
}: {
  icon: typeof Sun;
  label: string;
  isActive: boolean;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={cn(
        "flex flex-col items-center gap-2 p-4 rounded-lg border-2 transition-colors",
        isActive
          ? "border-primary-500 bg-primary-50 dark:bg-primary-900/20"
          : "border-gray-200 dark:border-gray-800 hover:border-gray-300 dark:hover:border-gray-700"
      )}
    >
      <Icon
        className={cn(
          "h-5 w-5",
          isActive
            ? "text-primary-600 dark:text-primary-400"
            : "text-gray-500 dark:text-gray-400"
        )}
      />
      <span
        className={cn(
          "text-sm font-medium",
          isActive
            ? "text-primary-600 dark:text-primary-400"
            : "text-gray-600 dark:text-gray-400"
        )}
      >
        {label}
      </span>
    </button>
  );
}
