"use client";

import { FolderPlus, ListMusic, Plus, Play, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn, formatDuration } from "@/lib/utils";
import type { Playlist, Track } from "@/types/music";

interface PlaylistSidebarProps {
  playlists: Playlist[];
  activePlaylistId: number | null;
  recentlyAdded: Track[];
  search: string;
  scanning: boolean;
  onSearch: (value: string) => void;
  onSelectPlaylist: (playlist: Playlist) => void;
  onCreatePlaylist: () => void;
  onScanFolder: () => void;
  onRenamePlaylist: (playlist: Playlist, name: string) => void;
  onDeletePlaylist: (playlist: Playlist) => void;
  onPlayPlaylist: (playlist: Playlist) => void;
  onClose?: () => void;
  showCloseButton?: boolean;
  className?: string;
}

export function PlaylistSidebar({
  playlists,
  activePlaylistId,
  recentlyAdded,
  search,
  scanning,
  onSearch,
  onSelectPlaylist,
  onCreatePlaylist,
  onScanFolder,
  onRenamePlaylist,
  onDeletePlaylist,
  onPlayPlaylist,
  onClose,
  showCloseButton,
  className
}: PlaylistSidebarProps) {
  return (
    <aside className={cn("flex min-h-0 shrink-0 flex-col", className)}>
      <div className={cn("drag-region flex items-center gap-3 border-b border-white/10 px-4 pb-4", 
        showCloseButton ? "pt-10" : "pt-4"
      )}>
        {showCloseButton && onClose && (
          <div className="no-drag">
            <Button variant="ghost" size="icon" onClick={onClose} title="Collapse sidebar" className="h-8 w-8">
              <X className="h-5 w-5" />
            </Button>
          </div>
        )}
        <div className="flex-1 min-w-0">
          <h1 className="text-base font-bold leading-tight truncate">Playlist</h1>
          <p className="text-xs text-white/74">
            {playlists.reduce((sum, playlist) => sum + playlist.track_count, 0)} tracks cached
          </p>
        </div>
        <div className="no-drag flex items-center gap-2">
          <Button variant="ghost" size="icon" onClick={onScanFolder} disabled={scanning} title="Scan folder">
            <FolderPlus className="h-5 w-5" />
          </Button>
          <Button variant="ghost" size="icon" onClick={onCreatePlaylist} title="Create playlist">
            <Plus className="h-5 w-5" />
          </Button>
        </div>
      </div>

      <div className="space-y-3 px-4 py-4">
        <div className="grid gap-1">
          {playlists.map((playlist) => (
            <div
              key={playlist.id}
              className={cn(
                "grid grid-cols-[auto_1fr_auto] items-center gap-3 rounded-md px-3 py-2 transition",
                activePlaylistId === playlist.id ? "bg-white/16" : "hover:bg-white/8"
              )}
            >
              <ListMusic className="h-5 w-5 text-white/78" />
              <button type="button" className="min-w-0 text-left" onClick={() => onSelectPlaylist(playlist)}>
                <span className="block truncate text-sm font-semibold">{playlist.name}</span>
                <span className="block truncate text-xs text-white/62">
                  {playlist.track_count} tracks, {formatDuration(playlist.duration)}
                </span>
              </button>
              {playlist.name !== "Library" ? (
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8 text-white/60 hover:text-white"
                  onClick={(e) => {
                    e.stopPropagation();
                    onPlayPlaylist(playlist);
                  }}
                  title="Play playlist"
                >
                  <Play className="h-4 w-4 fill-current pl-0.5" />
                </Button>
              ) : (
                <div className="w-8 h-8" />
              )}
            </div>
          ))}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-auto px-4 pb-4">
        <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-white/42">Recently added</div>
        <div className="space-y-1">
          {recentlyAdded.slice(0, 10).map((track) => (
            <div key={track.id} className="truncate rounded px-2 py-1 text-xs text-white/62">
              {track.title || track.file_name}
            </div>
          ))}
        </div>
      </div>
    </aside>
  );
}
