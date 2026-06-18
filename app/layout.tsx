import type { Metadata } from "next";
import localFont from "next/font/local";
import "./globals.css";

const googleSans = localFont({
  src: [
    {
      path: "../font/GoogleSans_17pt-Regular.ttf",
      weight: "400",
      style: "normal",
    },
    {
      path: "../font/GoogleSans_17pt-Medium.ttf",
      weight: "500",
      style: "normal",
    },
  ],
  variable: "--font-google-sans",
});

export const metadata: Metadata = {
  title: "Amberol Windows",
  description: "Offline desktop music player"
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`dark ${googleSans.variable}`}>
      <body className="font-sans">{children}</body>
    </html>
  );
}
