"use client";

import { Music2 } from "lucide-react";
import { cn } from "@/lib/utils";

interface AlbumArtProps {
  src?: string | null;
  title: string;
  className?: string;
}

export function AlbumArt({ src, title, className }: AlbumArtProps) {
  return (
    <div
      className={cn(
        "relative grid aspect-square place-items-center overflow-hidden rounded-lg bg-black/28 shadow-album",
        className
      )}
    >
      {src ? (
        <img src={src} alt={title} className="h-full w-full object-cover" loading="lazy" />
      ) : (
        <div className="grid h-full w-full place-items-center bg-gradient-to-br from-white/16 to-black/20">
          <Music2 className="h-12 w-12 text-white/60" />
        </div>
      )}
    </div>
  );
}
