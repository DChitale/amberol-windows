"use client";

import {
  Gauge,
  List,
  Pause,
  Play,
  Repeat,
  Repeat1,
  Shuffle,
  SkipBack,
  SkipForward,
  Volume1,
  Volume2,
  Sidebar
} from "lucide-react";
import { AlbumArt } from "@/components/album-art";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Slider } from "@/components/ui/slider";
import { Waveform } from "@/components/waveform";
import { albumForTrack, artistForTrack, formatTime, titleForTrack, cn } from "@/lib/utils";
import type { RepeatMode, Track } from "@/types/music";

interface NowPlayingProps {
  track: Track | null;
  audioRef: React.RefObject<HTMLAudioElement>;
  isPlaying: boolean;
  volume: number;
  speed: number;
  progress: number;
  duration: number;
  shuffle: boolean;
  repeat: RepeatMode;
  onToggle: () => void;
  onNext: () => void;
  onPrevious: () => void;
  onSeek: (seconds: number) => void;
  onVolume: (value: number) => void;
  onSpeed: (value: number) => void;
  onShuffle: () => void;
  onRepeat: () => void;
  sidebarOpen: boolean;
  onToggleSidebar: () => void;
  queueOpen: boolean;
  onToggleQueue: () => void;
}

export function NowPlaying({
  track,
  audioRef,
  isPlaying,
  volume,
  speed,
  progress,
  duration,
  shuffle,
  repeat,
  onToggle,
  onNext,
  onPrevious,
  onSeek,
  onVolume,
  onSpeed,
  onShuffle,
  onRepeat,
  sidebarOpen,
  onToggleSidebar,
  queueOpen,
  onToggleQueue
}: NowPlayingProps) {
  const title = track ? titleForTrack(track) : "No track selected";
  const artist = track ? artistForTrack(track) : "Scan a folder to begin";
  const album = track ? albumForTrack(track) : "Offline Library";

  return (
    <main className="drag-region flex min-h-0 flex-1 flex-col items-center justify-center px-6 py-6">
      <AlbumArt src={track?.cover_art} title={title} className="w-[180px] h-[180px] rounded-xl shadow-lg" />

      <div className="mt-6 w-full max-w-[320px] no-drag">
        <Waveform audioRef={audioRef} progress={progress} duration={duration} onSeek={onSeek} trackId={track?.id ?? 0} />
        <div className="mt-1 flex justify-between text-[11px] tabular-nums text-white/78">
          <span>{formatTime(progress)}</span>
          <span>{formatTime(duration)}</span>
        </div>
      </div>

      <div className="mt-5 w-full max-w-[300px] text-center">
        <h2 className="line-clamp-2 text-xl font-extrabold leading-snug">{title}</h2>
        <p className="mt-0.5 line-clamp-2 text-sm text-white/80 leading-normal">{artist}</p>
        <p className="mt-0.5 line-clamp-1 text-xs text-white/60">{album}</p>
      </div>

      <div className="mt-6 flex items-center gap-4 no-drag">
        <Button 
          variant="ghost" 
          size="icon"
          className="h-10 w-10 rounded-full bg-white/10 hover:bg-white/18 text-white hover:scale-105 active:scale-95 transition-all flex items-center justify-center border border-white/5" 
          onClick={onPrevious} 
          disabled={!track} 
          title="Previous"
        >
          <SkipBack className="h-4 w-4 fill-current text-white" />
        </Button>
        <Button 
          variant="ghost" 
          size="icon"
          className="h-14 w-14 rounded-full bg-white/20 hover:bg-white/28 text-white hover:scale-105 active:scale-95 transition-all flex items-center justify-center border border-white/8 shadow-md" 
          onClick={onToggle} 
          disabled={!track} 
          title={isPlaying ? "Pause" : "Play"}
        >
          {isPlaying ? <Pause className="h-6 w-6 fill-current text-white" /> : <Play className="h-6 w-6 fill-current text-white pl-0.5" />}
        </Button>
        <Button 
          variant="ghost" 
          size="icon"
          className="h-10 w-10 rounded-full bg-white/10 hover:bg-white/18 text-white hover:scale-105 active:scale-95 transition-all flex items-center justify-center border border-white/5" 
          onClick={onNext} 
          disabled={!track} 
          title="Next"
        >
          <SkipForward className="h-4 w-4 fill-current text-white" />
        </Button>
      </div>

      <div className="mt-5 flex w-full max-w-[260px] items-center gap-3 no-drag">
        <Volume1 className="h-4 w-4 text-white/60" />
        <Slider value={[volume]} max={1} step={0.01} onValueChange={([value]) => onVolume(value ?? volume)} />
        <Volume2 className="h-4 w-4 text-white/60" />
      </div>

      <div className="mt-5 flex items-center gap-3 no-drag">
        <Button 
          variant="ghost" 
          size="icon"
          className={cn(
            "h-9 w-9 rounded-full flex items-center justify-center hover:scale-105 active:scale-95 transition-all border",
            sidebarOpen 
              ? "bg-white/24 text-white border-white/12 shadow-sm" 
              : "bg-white/10 hover:bg-white/18 text-white/80 hover:text-white border-white/5"
          )}
          onClick={onToggleSidebar} 
          title="Toggle Sidebar"
        >
          <Sidebar className="h-[18px] w-[18px]" />
        </Button>
        <Button 
          variant="ghost" 
          size="icon"
          className={cn(
            "h-9 w-9 rounded-full flex items-center justify-center hover:scale-105 active:scale-95 transition-all border",
            shuffle 
              ? "bg-white/24 text-white border-white/12 shadow-sm" 
              : "bg-white/10 hover:bg-white/18 text-white/80 hover:text-white border-white/5"
          )}
          onClick={onShuffle} 
          title="Shuffle"
        >
          <Shuffle className="h-[18px] w-[18px]" />
        </Button>
        <Button 
          variant="ghost" 
          size="icon"
          className={cn(
            "h-9 w-9 rounded-full flex items-center justify-center hover:scale-105 active:scale-95 transition-all border",
            repeat !== "off" 
              ? "bg-white/24 text-white border-white/12 shadow-sm" 
              : "bg-white/10 hover:bg-white/18 text-white/80 hover:text-white border-white/5"
          )}
          onClick={onRepeat} 
          title={`Repeat ${repeat}`}
        >
          {repeat === "one" ? <Repeat1 className="h-[18px] w-[18px]" /> : <Repeat className="h-[18px] w-[18px]" />}
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button 
              variant="ghost" 
              size="icon"
              className="h-9 w-9 rounded-full bg-white/10 hover:bg-white/18 text-white/80 hover:text-white hover:scale-105 active:scale-95 transition-all flex items-center justify-center border border-white/5" 
              title="Playback speed"
            >
              <Gauge className="h-[18px] w-[18px]" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent>
            {[0.5, 0.75, 1, 1.25, 1.5, 2].map((value) => (
              <DropdownMenuItem key={value} onClick={() => onSpeed(value)}>
                {value}x{speed === value ? " selected" : ""}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
        <Button 
          variant="ghost" 
          size="icon"
          className={cn(
            "h-9 w-9 rounded-full flex items-center justify-center hover:scale-105 active:scale-95 transition-all border",
            queueOpen 
              ? "bg-white/24 text-white border-white/12 shadow-sm" 
              : "bg-white/10 hover:bg-white/18 text-white/80 hover:text-white border-white/5"
          )}
          onClick={onToggleQueue} 
          title="Toggle Queue"
        >
          <List className="h-[18px] w-[18px]" />
        </Button>
      </div>
    </main>
  );
}
