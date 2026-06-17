"use client";

import { useMemo, useRef } from "react";
import { closestCenter, DndContext, type DragEndEvent } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { useVirtualizer } from "@tanstack/react-virtual";
import { TrackRow } from "@/components/track-row";
import { Button } from "@/components/ui/button";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";
import { artistForTrack, titleForTrack } from "@/lib/utils";
import type { Playlist, Track } from "@/types/music";

interface QueuePanelProps {
  tracks: Track[];
  currentTrack: Track | null;
  playlists: Playlist[];
  search: string;
  onPlay: (track: Track) => void;
  onReorder: (activeId: number, overId: number) => void;
  onAddToPlaylist: (playlist: Playlist, track: Track) => void;
  onRemove?: (track: Track) => void;
  onClose?: () => void;
  showCloseButton?: boolean;
  className?: string;
}

export function QueuePanel({
  tracks,
  currentTrack,
  playlists,
  search,
  onPlay,
  onReorder,
  onAddToPlaylist,
  onRemove,
  onClose,
  showCloseButton,
  className
}: QueuePanelProps) {
  const parentRef = useRef<HTMLDivElement>(null);
  const filteredTracks = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) {
      return tracks;
    }

    return tracks.filter((track) =>
      [titleForTrack(track), artistForTrack(track), track.album ?? ""].some((value) => value.toLowerCase().includes(needle))
    );
  }, [search, tracks]);

  const virtualizer = useVirtualizer({
    count: filteredTracks.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 68,
    overscan: 8
  });

  function onDragEnd(event: DragEndEvent) {
    const activeId = Number(event.active.id);
    const overId = Number(event.over?.id);
    if (activeId && overId && activeId !== overId) {
      onReorder(activeId, overId);
    }
  }

  return (
    <section className={cn("flex min-h-0 shrink-0 flex-col", className)}>
      <div className={cn("drag-region flex items-center gap-3 border-b border-white/10 px-4 pb-4", 
        showCloseButton ? "pt-10" : "pt-4"
      )}>
        {showCloseButton && onClose && (
          <div className="no-drag">
            <Button variant="ghost" size="icon" onClick={onClose} title="Collapse queue" className="h-8 w-8">
              <X className="h-5 w-5" />
            </Button>
          </div>
        )}
        <div className="flex-1 min-w-0">
          <h2 className="text-base font-bold leading-tight truncate">Queue</h2>
          <p className="text-xs text-white/68">{filteredTracks.length} tracks</p>
        </div>
      </div>
      <div ref={parentRef} className="min-h-0 flex-1 overflow-auto px-3 py-3">
        <DndContext collisionDetection={closestCenter} onDragEnd={onDragEnd}>
          <SortableContext items={filteredTracks.map((track) => track.id)} strategy={verticalListSortingStrategy}>
            <div style={{ height: `${virtualizer.getTotalSize()}px`, position: "relative" }}>
              {virtualizer.getVirtualItems().map((virtualRow) => {
                const track = filteredTracks[virtualRow.index];
                if (!track) {
                  return null;
                }

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
                      onPlay={onPlay}
                      onAddToPlaylist={onAddToPlaylist}
                      onRemove={onRemove}
                    />
                  </div>
                );
              })}
            </div>
          </SortableContext>
        </DndContext>
      </div>
    </section>
  );
}
