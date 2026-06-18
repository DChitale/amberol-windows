"use client";

import { Play } from "lucide-react";
import { Button } from "@/components/ui/button";

interface WelcomeScreenProps {
  onScanFolder: () => void;
  scanning: boolean;
}

export function WelcomeScreen({ onScanFolder, scanning }: WelcomeScreenProps) {
  return (
    <main className="drag-region flex flex-1 flex-col items-center justify-center px-6 py-12">
      {/* Blue play icon logo */}
      <div className="flex h-28 w-28 items-center justify-center rounded-full bg-gradient-to-tr from-blue-600 to-sky-400 shadow-xl shadow-blue-500/20 border border-blue-400/20 hover:scale-105 transition-all duration-300">
        <Play className="h-12 w-12 fill-white text-white ml-1.5" />
      </div>

      <h1 className="mt-8 text-2xl font-bold tracking-tight text-white text-center">
        Amberol
      </h1>

      <p className="mt-4 text-sm text-white/60 text-center max-w-[340px] leading-relaxed">
        Select a file or a folder, or drag files from your file manager to the application window to add songs to the playlist
      </p>

      <div className="mt-8 flex flex-col gap-3 w-full max-w-[200px] no-drag">
        <Button
          onClick={onScanFolder}
          disabled={scanning}
          className="w-full py-5 rounded-full bg-blue-600 hover:bg-blue-500 text-white font-bold text-sm shadow-md transition-all hover:scale-105 active:scale-95 border border-blue-500/30"
        >
          {scanning ? "Scanning..." : "Add Folder"}
        </Button>
        <Button
          onClick={onScanFolder}
          disabled={scanning}
          variant="ghost"
          className="w-full py-5 rounded-full bg-white/8 hover:bg-white/12 text-white/80 font-bold text-sm transition-all hover:scale-105 active:scale-95 border border-white/5"
        >
          Add Song
        </Button>
      </div>
    </main>
  );
}
