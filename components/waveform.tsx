"use client";

import { useEffect, useMemo, useRef } from "react";
import { cn } from "@/lib/utils";

interface WaveformProps {
  audioRef: React.RefObject<HTMLAudioElement>;
  progress: number;
  duration: number;
  onSeek: (seconds: number) => void;
  className?: string;
  trackId?: number;
}

export function Waveform({ audioRef, progress, duration, onSeek, className, trackId = 0 }: WaveformProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  // Generate a static, unique, deterministic shape for each song
  const waveformHeights = useMemo(() => {
    const count = 76;
    let seed = (trackId * 1000 + Math.floor(duration)) || 42;
    const nextRandom = () => {
      const x = Math.sin(seed++) * 10000;
      return x - Math.floor(x);
    };

    const heights: number[] = [];
    for (let i = 0; i < count; i++) {
      const r = nextRandom();
      // Taper the ends to look like a bell curve soundwave
      const centerFactor = 1 - Math.pow(Math.abs(i - count / 2) / (count / 2), 1.8);
      const val = (0.15 + r * 0.85) * centerFactor;
      heights.push(Math.max(0.06, val));
    }
    return heights;
  }, [trackId, duration]);

  // Butter-smooth, 60fps requestAnimationFrame redraw loop
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }

    let animationId: number;

    const render = () => {
      const rect = canvas.getBoundingClientRect();
      if (rect.width === 0 || rect.height === 0) {
        animationId = requestAnimationFrame(render);
        return;
      }

      const dpr = window.devicePixelRatio || 1;
      const targetWidth = Math.max(1, Math.floor(rect.width * dpr));
      const targetHeight = Math.max(1, Math.floor(rect.height * dpr));

      if (canvas.width !== targetWidth || canvas.height !== targetHeight) {
        canvas.width = targetWidth;
        canvas.height = targetHeight;
      }

      const ctx = canvas.getContext("2d");
      if (!ctx) {
        animationId = requestAnimationFrame(render);
        return;
      }

      ctx.save();
      ctx.scale(dpr, dpr);
      ctx.clearRect(0, 0, rect.width, rect.height);

      // Read audio element directly for real-time playhead alignment
      const audio = audioRef?.current;
      const curTime = audio ? audio.currentTime : progress;
      const totalTime = duration > 0 ? duration : 1;
      const progressRatio = Math.min(1, Math.max(0, curTime / totalTime));
      const progressX = progressRatio * rect.width;

      const count = waveformHeights.length;
      const barWidth = rect.width / count;
      const gap = 3;
      const widthToDraw = Math.max(2, barWidth - gap);

      for (let index = 0; index < count; index += 1) {
        const value = waveformHeights[index] || 0.1;
        const height = Math.max(4, value * rect.height * 0.85);
        const x = index * barWidth;
        const y = (rect.height - height) / 2;

        const barLeft = x + 1;
        const barRight = barLeft + widthToDraw;

        if (barRight <= progressX) {
          // Fully elapsed: show active color with white glow
          ctx.shadowColor = "rgba(255, 255, 255, 0.55)";
          ctx.shadowBlur = 6;
          ctx.fillStyle = "rgba(255, 255, 255, 0.95)";
          ctx.fillRect(barLeft, y, widthToDraw, height);
        } else if (barLeft >= progressX) {
          // Fully unplayed: show dim inactive color
          ctx.shadowColor = "transparent";
          ctx.shadowBlur = 0;
          ctx.fillStyle = "rgba(255, 255, 255, 0.24)";
          ctx.fillRect(barLeft, y, widthToDraw, height);
        } else {
          // Partially elapsed transition bar
          const elapsedWidth = progressX - barLeft;
          const remainingWidth = widthToDraw - elapsedWidth;

          // Draw inactive/remaining part
          ctx.shadowColor = "transparent";
          ctx.shadowBlur = 0;
          ctx.fillStyle = "rgba(255, 255, 255, 0.24)";
          ctx.fillRect(barLeft + elapsedWidth, y, remainingWidth, height);

          // Draw active/elapsed part
          ctx.shadowColor = "rgba(255, 255, 255, 0.55)";
          ctx.shadowBlur = 6;
          ctx.fillStyle = "rgba(255, 255, 255, 0.95)";
          ctx.fillRect(barLeft, y, elapsedWidth, height);
        }
      }

      ctx.restore();
      animationId = requestAnimationFrame(render);
    };

    render();

    return () => {
      cancelAnimationFrame(animationId);
    };
  }, [audioRef, progress, duration, waveformHeights]);

  function seek(event: React.PointerEvent<HTMLButtonElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    const ratio = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    onSeek(ratio * duration);
  }

  return (
    <button type="button" className={cn("block h-16 w-full cursor-pointer", className)} onPointerDown={seek}>
      <canvas ref={canvasRef} className="h-full w-full" />
    </button>
  );
}
