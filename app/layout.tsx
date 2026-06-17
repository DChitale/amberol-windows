import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Amberol Windows",
  description: "Offline desktop music player"
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className="dark">
      <body>{children}</body>
    </html>
  );
}
