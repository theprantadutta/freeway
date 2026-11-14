import type { Metadata } from "next";
import { Overpass, Overpass_Mono } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";

// Overpass is an open descendant of Highway Gothic, the US road-sign alphabet. A
// gateway named for a highway reads its numbers off the same signage: open, upright
// figures that stay legible at a glance and at a distance.
const sans = Overpass({
  variable: "--font-sans",
  subsets: ["latin"],
  display: "swap",
});

// Only for things that are literally code: API keys and request snippets.
const mono = Overpass_Mono({
  variable: "--font-mono",
  subsets: ["latin"],
  display: "swap",
});

export const metadata: Metadata = {
  title: { default: "Freeway", template: "%s · Freeway" },
  description: "Telemetry and routing for the Freeway AI gateway",
  applicationName: "Freeway",
  // icon.svg, favicon.ico and apple-icon.png sit beside this file and are picked
  // up by convention; this only adds the mask icon older Safari asks for.
  icons: { other: [{ rel: "mask-icon", url: "/icon-192.png", color: "#199E70" }] },
};

export const viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: dark)", color: "#0B1324" },
    { media: "(prefers-color-scheme: light)", color: "#F3F5F9" },
  ],
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        {/* Settle the theme before first paint so there is no flash of the wrong
            one. The console is dark by default and `.light` opts out, so that is
            the class this sets. */}
        <script
          dangerouslySetInnerHTML={{
            __html: `(function(){try{var s=localStorage.getItem('freeway-theme');var t=s?JSON.parse(s).state.theme:'system';var d=t==='dark'||(t!=='light'&&matchMedia('(prefers-color-scheme: dark)').matches);document.documentElement.classList.toggle('light',!d)}catch(e){}})()`,
          }}
        />
      </head>
      <body className={`${sans.variable} ${mono.variable} antialiased`}>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
