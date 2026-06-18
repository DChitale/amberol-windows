"use client";

import { CSS } from "@dnd-kit/utilities";
import { useSortable } from "@dnd-kit/sortable";
import { GripVertical, MoreHorizontal, Volume2, Trash2 } from "lucide-react";
import { AlbumArt } from "@/components/album-art";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { cn, artistForTrack, formatTime, titleForTrack } from "@/lib/utils";
import type { Playlist, Track } from "@/types/music";

interface TrackRowProps {
  track: Track;
  active?: boolean;
  draggable?: boolean;
  playlists?: Playlist[];
  onPlay: (track: Track) => void;
  onAddToPlaylist?: (playlist: Playlist, track: Track) => void;
  onRemove?: (track: Track) => void;
}

export function TrackRow({ track, active, draggable = false, playlists = [], onPlay, onAddToPlaylist, onRemove }: TrackRowProps) {
  const sortable = useSortable({ id: track.id, disabled: !draggable });
  const style = {
    transform: CSS.Transform.toString(sortable.transform),
    transition: sortable.transition
  };

  return (
    <div
      ref={sortable.setNodeRef}
      style={style}
      className={cn(
        "group grid h-[64px] grid-cols-[auto_1fr_auto] items-center gap-3 rounded-md px-2 py-2 text-left transition no-drag",
        active ? "bg-white/14" : "hover:bg-white/8",
        sortable.isDragging && "z-10 bg-white/16 opacity-80"
      )}
    >
      <button
        type="button"
        className="grid h-12 w-12 shrink-0 place-items-center overflow-hidden rounded-md"
        onClick={() => onPlay(track)}
      >
        <AlbumArt src={track.cover_art} title={titleForTrack(track)} className="h-12 w-12 rounded-md shadow-none" />
      </button>
      <button type="button" className="min-w-0 text-left" onClick={() => onPlay(track)}>
        <div className="truncate text-sm font-semibold text-white">{titleForTrack(track)}</div>
        <div className="truncate text-xs text-white/72">{artistForTrack(track)}</div>
      </button>
      <div className="flex items-center gap-2 text-white/70">
        {active ? <Volume2 className="h-4 w-4" /> : <span className="hidden text-xs tabular-nums sm:block">{formatTime(track.duration)}</span>}
        {playlists.length > 0 && onAddToPlaylist ? (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="h-7 w-7 opacity-0 transition group-hover:opacity-100" title="Track actions">
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {playlists.map((playlist) => (
                <DropdownMenuItem key={playlist.id} onClick={() => onAddToPlaylist(playlist, track)}>
                  Add to {playlist.name}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null}
        {onRemove ? (
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7 opacity-0 transition group-hover:opacity-100 text-white/60 hover:text-red-400 hover:bg-red-500/10"
            title="Remove from queue"
            onClick={(e) => {
              e.stopPropagation();
              onRemove(track);
            }}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        ) : null}
        {draggable ? (
          <button
            type="button"
            className="cursor-grab rounded p-1 opacity-45 transition hover:bg-white/10 hover:opacity-100"
            {...sortable.attributes}
            {...sortable.listeners}
          >
            <GripVertical className="h-4 w-4" />
          </button>
        ) : null}
      </div>
    </div>
  );
}
