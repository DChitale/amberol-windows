"use client";

import { useMemo } from "react";
import {
  Gauge,
  Pause,
  Play,
  Repeat,
  Repeat1,
  Shuffle,
  SkipBack,
  SkipForward,
  Volume1,
  Volume2,
  Sidebar,
  Check,
  Sparkles,
  Disc,
  Star,
  Music,
  Radio
} from "lucide-react";
import { AlbumArt } from "@/components/album-art";
import { ScrollingText } from "@/components/scrolling-text";
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
  onToggleSidebar
}: NowPlayingProps) {
  const title = track ? titleForTrack(track) : "No track selected";
  const artist = track ? artistForTrack(track) : "Scan a folder to begin";
  const album = track ? albumForTrack(track) : "Offline Library";

  const qualityInfo = useMemo(() => {
    if (!track) return null;
    
    const ext = track.path.split(".").pop()?.toUpperCase() || "AUDIO";
    if (track.size_bytes && track.duration) {
      const bitrateKbps = Math.round((track.size_bytes * 8) / (track.duration * 1000));
      
      let colorClass = "text-sky-400 drop-shadow-[0_0_4px_rgba(56,189,248,0.4)]";
      let iconType = "standard";
      
      if (ext === "FLAC" || ext === "WAV") {
        if (bitrateKbps > 1000) {
          colorClass = "text-emerald-400 drop-shadow-[0_0_4px_rgba(52,211,153,0.5)]";
          iconType = "hires";
        } else {
          colorClass = "text-teal-400 drop-shadow-[0_0_4px_rgba(45,212,191,0.5)]";
          iconType = "lossless";
        }
      } else if (bitrateKbps >= 320) {
        colorClass = "text-amber-400 drop-shadow-[0_0_4px_rgba(251,191,36,0.5)]";
        iconType = "hq";
      } else if (bitrateKbps >= 192) {
        colorClass = "text-sky-400 drop-shadow-[0_0_4px_rgba(56,189,248,0.4)]";
        iconType = "standard";
      } else {
        colorClass = "text-slate-400";
        iconType = "basic";
      }
      
      return {
        label: `${bitrateKbps} kbps`,
        colorClass,
        iconType
      };
    }
    
    return {
      label: "Standard Quality",
      colorClass: "text-sky-400 drop-shadow-[0_0_4px_rgba(56,189,248,0.4)]",
      iconType: "standard"
    };
  }, [track]);

  return (
    <main className="drag-region flex min-h-0 flex-1 flex-col items-center justify-center px-6 py-6">
      <AlbumArt src={track?.cover_art} title={title} className="w-[180px] h-[180px] rounded-xl shadow-lg" />

      <div className="mt-6 w-full max-w-[320px] no-drag">
        <Waveform audioRef={audioRef} progress={progress} duration={duration} onSeek={onSeek} trackId={track?.id ?? 0} />
        <div className="mt-1 flex justify-between text-[11px] tabular-nums text-white/78">
          <span>{formatTime(progress)}</span>
          <span>{duration > 0 ? `-${formatTime(Math.max(0, duration - progress))}` : "0:00"}</span>
        </div>
        {qualityInfo && (
          <div className="mt-2.5 flex items-center justify-center">
            <span className="inline-flex items-center gap-1.5 rounded-full bg-white/6 border border-white/8 px-2.5 py-0.5 text-[9px] font-bold uppercase tracking-wider text-white/70 shadow-sm">
              {qualityInfo.iconType === "hires" && <Sparkles className={cn("h-3 w-3", qualityInfo.colorClass)} />}
              {qualityInfo.iconType === "lossless" && <Disc className={cn("h-3 w-3", qualityInfo.colorClass)} />}
              {qualityInfo.iconType === "hq" && <Star className={cn("h-3 w-3", qualityInfo.colorClass)} />}
              {qualityInfo.iconType === "standard" && <Music className={cn("h-3 w-3", qualityInfo.colorClass)} />}
              {qualityInfo.iconType === "basic" && <Radio className={cn("h-3 w-3", qualityInfo.colorClass)} />}
              {qualityInfo.label}
            </span>
          </div>
        )}
      </div>

      <div className="mt-4 w-full max-w-[300px] text-center">
        <ScrollingText text={title} className="text-xl font-extrabold leading-snug block" />
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
            "h-9 w-9 rounded-full flex items-center justify-center hover:scale-105 active:scale-95 transition-all",
            sidebarOpen 
              ? "bg-white/24 border border-white/12 text-white shadow-sm" 
              : "bg-transparent border border-transparent text-white/60 hover:bg-white/10 hover:text-white"
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
            "h-9 w-9 rounded-full flex items-center justify-center hover:scale-105 active:scale-95 transition-all",
            shuffle 
              ? "bg-white/24 border border-white/12 text-white shadow-sm" 
              : "bg-transparent border border-transparent text-white/60 hover:bg-white/10 hover:text-white"
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
            "h-9 w-9 rounded-full flex items-center justify-center hover:scale-105 active:scale-95 transition-all",
            repeat !== "off" 
              ? "bg-white/24 border border-white/12 text-white shadow-sm" 
              : "bg-transparent border border-transparent text-white/60 hover:bg-white/10 hover:text-white"
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
              className={cn(
                "h-9 w-9 rounded-full flex items-center justify-center hover:scale-105 active:scale-95 transition-all",
                speed !== 1
                  ? "bg-white/24 border border-white/12 text-white shadow-sm" 
                  : "bg-transparent border border-transparent text-white/60 hover:bg-white/10 hover:text-white"
              )}
              title="Playback speed"
            >
              <Gauge className="h-[18px] w-[18px]" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-[100px]">
            {[0.5, 0.75, 1, 1.25, 1.5, 2].map((value) => (
              <DropdownMenuItem 
                key={value} 
                onClick={() => onSpeed(value)}
                className="flex items-center justify-between text-xs cursor-pointer"
              >
                <span>{value}x</span>
                {speed === value && <Check className="h-3.5 w-3.5 text-white/80" />}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>

      </div>
    </main>
  );
}
