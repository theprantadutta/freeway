import type { Metadata } from "next";
import { Space_Grotesk, JetBrains_Mono } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";

// Space Grotesk carries the interface: it has real character at display sizes and
// stays technical rather than friendly, which suits a console.
const display = Space_Grotesk({
  variable: "--font-display",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  display: "swap",
});

// Every number and identifier is set in mono. In an ops console figures are data to
// be compared down a column, not prose.
const mono = JetBrains_Mono({
  variable: "--font-mono",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  display: "swap",
});

export const metadata: Metadata = {
  title: { default: "Freeway", template: "%s · Freeway" },
  description: "Telemetry and routing for the Freeway AI gateway",
  applicationName: "Freeway",
  // icon.svg, favicon.ico and apple-icon.png sit beside this file and are picked
  // up by convention; this only adds the mask icon older Safari asks for.
  icons: { other: [{ rel: "mask-icon", url: "/icon-192.png", color: "#10B981" }] },
};

export const viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: dark)", color: "#07090D" },
    { media: "(prefers-color-scheme: light)", color: "#FAFAFC" },
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
      <body className={`${display.variable} ${mono.variable} antialiased`}>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
