"use client";

import { Minus, Square, X } from "lucide-react";
import { Button } from "@/components/ui/button";

export function WindowControls() {
  const handleMinimize = async () => {
    if (typeof window !== "undefined") {
      try {
        const { getCurrentWindow } = await import("@tauri-apps/api/window");
        await getCurrentWindow().minimize();
      } catch (err) {
        console.error("Failed to minimize window:", err);
      }
    }
  };

  const handleMaximize = async () => {
    if (typeof window !== "undefined") {
      try {
        const { getCurrentWindow } = await import("@tauri-apps/api/window");
        await getCurrentWindow().toggleMaximize();
      } catch (err) {
        console.error("Failed to maximize window:", err);
      }
    }
  };

  const handleClose = async () => {
    if (typeof window !== "undefined") {
      try {
        const { getCurrentWindow } = await import("@tauri-apps/api/window");
        await getCurrentWindow().close();
      } catch (err) {
        console.error("Failed to close window:", err);
      }
    }
  };

  return (
    <div className="no-drag flex items-center">
      <Button
        className="h-8 w-10 rounded-none bg-transparent text-white/70 hover:bg-white/10 hover:text-white"
        variant="ghost"
        size="icon"
        onClick={handleMinimize}
        title="Minimize"
      >
        <Minus className="h-4 w-4" />
      </Button>
      <Button
        className="h-8 w-10 rounded-none bg-transparent text-white/70 hover:bg-white/10 hover:text-white"
        variant="ghost"
        size="icon"
        onClick={handleMaximize}
        title="Maximize"
      >
        <Square className="h-3.5 w-3.5" />
      </Button>
      <Button
        className="h-8 w-10 rounded-none bg-transparent text-white/70 hover:bg-red-500/80 hover:text-white"
        variant="ghost"
        size="icon"
        onClick={handleClose}
        title="Close"
      >
        <X className="h-4 w-4" />
      </Button>
    </div>
  );
}
