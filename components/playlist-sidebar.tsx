"use client";

import { useRef } from "react";
import { closestCenter, DndContext, type DragEndEvent } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { useVirtualizer } from "@tanstack/react-virtual";
import { TrackRow } from "@/components/track-row";
import { FolderPlus, ListMusic, Plus, Play, X, RefreshCw, Music } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn, formatDuration } from "@/lib/utils";
import type { Playlist, Track } from "@/types/music";

interface PlaylistSidebarProps {
  playlists: Playlist[];
  activePlaylistId: number | null;
  lastScannedFolder: string;
  scanning: boolean;
  onSelectPlaylist: (playlist: Playlist) => void;
  onCreatePlaylist: () => void;
  onScanFolder: () => void;
  onRefreshFolder: () => void;
  onRenamePlaylist: (playlist: Playlist, name: string) => void;
  onDeletePlaylist: (playlist: Playlist) => void;
  onPlayPlaylist: (playlist: Playlist) => void;
  onClose?: () => void;
  showCloseButton?: boolean;
  className?: string;

  // Queue integration props
  queueTracks: Track[];
  currentTrack: Track | null;
  onPlayTrack: (track: Track) => void;
  onReorderQueue: (activeId: number, overId: number) => void;
  onAddToPlaylist: (playlist: Playlist, track: Track) => void;
  onRemoveFromQueue: (track: Track) => void;
}

export function PlaylistSidebar({
  playlists,
  activePlaylistId,
  lastScannedFolder,
  scanning,
  onSelectPlaylist,
  onCreatePlaylist,
  onScanFolder,
  onRefreshFolder,
  onRenamePlaylist,
  onDeletePlaylist,
  onPlayPlaylist,
  onClose,
  showCloseButton,
  className,
  queueTracks,
  currentTrack,
  onPlayTrack,
  onReorderQueue,
  onAddToPlaylist,
  onRemoveFromQueue
}: PlaylistSidebarProps) {
  const parentRef = useRef<HTMLDivElement>(null);

  const virtualizer = useVirtualizer({
    count: queueTracks.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 64, // TrackRow height is h-[64px]
    overscan: 8
  });

  function onDragEnd(event: DragEndEvent) {
    const activeId = Number(event.active.id);
    const overId = Number(event.over?.id);
    if (activeId && overId && activeId !== overId) {
      onReorderQueue(activeId, overId);
    }
  }

  return (
    <aside className={cn("flex min-h-0 shrink-0 flex-col", className)}>
      {/* Header */}
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
          <h1 className="text-base font-bold leading-tight truncate">Library</h1>
          <p className="text-xs text-white/74 truncate" title={lastScannedFolder || undefined}>
            {lastScannedFolder 
              ? `${lastScannedFolder.split(/[/\\]/).pop() || lastScannedFolder} • ${playlists.find((p) => p.name === "Library")?.track_count ?? 0} tracks`
              : `${playlists.find((p) => p.name === "Library")?.track_count ?? 0} tracks cached`
            }
          </p>
        </div>
        <div className="no-drag flex items-center gap-2">
          <Button variant="ghost" size="icon" onClick={onScanFolder} disabled={scanning} title="Scan folder">
            <FolderPlus className="h-5 w-5" />
          </Button>
          <Button 
            variant="ghost" 
            size="icon" 
            onClick={onRefreshFolder} 
            disabled={scanning || !lastScannedFolder} 
            title={lastScannedFolder ? `Refresh folder: ${lastScannedFolder}` : "Refresh folder"}
          >
            <RefreshCw className={cn("h-5 w-5", scanning && "animate-spin")} />
          </Button>
          <Button variant="ghost" size="icon" onClick={onCreatePlaylist} title="Create playlist">
            <Plus className="h-5 w-5" />
          </Button>
        </div>
      </div>

      {/* Playlists List Section */}
      <div className="shrink-0 flex flex-col max-h-[180px] border-b border-white/10">
        <div className="px-4 pt-3 pb-1 text-[10px] font-bold uppercase tracking-wider text-white/40">
          Playlists
        </div>
        <div className="flex-1 overflow-y-auto px-4 pb-3 space-y-1">
          {playlists.map((playlist) => (
            <div
              key={playlist.id}
              className={cn(
                "grid grid-cols-[auto_1fr_auto] items-center gap-3 rounded-md px-3 py-1.5 transition",
                activePlaylistId === playlist.id ? "bg-white/16" : "hover:bg-white/8"
              )}
            >
              <ListMusic className="h-4 w-4 text-white/60" />
              <button type="button" className="min-w-0 text-left" onClick={() => onSelectPlaylist(playlist)}>
                <span className="block truncate text-xs font-semibold">
                  {playlist.name === "Library" && lastScannedFolder
                    ? `Library (${lastScannedFolder.split(/[/\\]/).pop() || lastScannedFolder})`
                    : playlist.name
                  }
                </span>
                <span className="block truncate text-[10px] text-white/50">
                  {playlist.track_count} tracks • {formatDuration(playlist.duration)}
                </span>
              </button>
              {playlist.name !== "Library" ? (
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6 text-white/40 hover:text-white"
                  onClick={(e) => {
                    e.stopPropagation();
                    onPlayPlaylist(playlist);
                  }}
                  title="Play playlist"
                >
                  <Play className="h-3 w-3 fill-current pl-0.5" />
                </Button>
              ) : (
                <div className="w-6 h-6" />
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Queue Section */}
      <div className="flex-1 min-h-0 flex flex-col pt-3">
        <div className="px-4 pb-2 flex items-center justify-between border-b border-white/5">
          <span className="text-[10px] font-bold uppercase tracking-wider text-white/40 flex items-center gap-1.5">
            <Music className="h-3 w-3" /> Queue
          </span>
          <span className="text-[10px] text-white/40 font-medium">{queueTracks.length} tracks</span>
        </div>

        {/* Virtualized list */}
        <div ref={parentRef} className="min-h-0 flex-1 overflow-auto px-3 py-3">
          {queueTracks.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-8 text-center text-xs text-white/40">
              No tracks in queue
            </div>
          ) : (
            <DndContext collisionDetection={closestCenter} onDragEnd={onDragEnd}>
              <SortableContext items={queueTracks.map((track) => track.id)} strategy={verticalListSortingStrategy}>
                <div style={{ height: `${virtualizer.getTotalSize()}px`, position: "relative" }}>
                  {virtualizer.getVirtualItems().map((virtualRow) => {
                    const track = queueTracks[virtualRow.index];
                    if (!track) return null;

                    return (
                      <div
                        key={track.id}
                        style={{
                          position: "absolute",
                          top: 0,
                          left: 0,
                          width: "100%",
                          transform: `translateY(${virtualRow.start}px)`
                        }}
                      >
                        <TrackRow
                          track={track}
                          active={currentTrack?.id === track.id}
                          draggable
                          playlists={playlists.filter((playlist) => playlist.name !== "Library")}
                          onPlay={onPlayTrack}
                          onAddToPlaylist={onAddToPlaylist}
                          onRemove={onRemoveFromQueue}
                        />
                      </div>
                    );
                  })}
                </div>
              </SortableContext>
            </DndContext>
          )}
        </div>
      </div>
    </aside>
  );
}
