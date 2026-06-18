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

  // Generate a static, unique, realistic peak shape for each song using harmonic waves
  const waveformHeights = useMemo(() => {
    const count = 76;
    let seed = (trackId * 1000 + Math.floor(duration)) || 42;
    const nextRandom = () => {
      const x = Math.sin(seed++) * 10000;
      return x - Math.floor(x);
    };

    const heights: number[] = [];
    
    // Create random wave harmonic parameters for a natural music-like soundwave look
    const h1Freq = 0.05 + nextRandom() * 0.05;
    const h2Freq = 0.1 + nextRandom() * 0.15;
    const h3Freq = 0.2 + nextRandom() * 0.3;
    
    const h1Amp = 0.3 + nextRandom() * 0.2;
    const h2Amp = 0.15 + nextRandom() * 0.15;
    const h3Amp = 0.05 + nextRandom() * 0.1;

    for (let i = 0; i < count; i++) {
      const base = 
        Math.sin(i * h1Freq) * h1Amp +
        Math.sin(i * h2Freq) * h2Amp +
        Math.cos(i * h3Freq) * h3Amp;
        
      const normalizedBase = (base + (h1Amp + h2Amp + h3Amp)) / ((h1Amp + h2Amp + h3Amp) * 2);
      const noise = nextRandom();
      
      // Compute final height [0.15, 0.95] (no tapering at the left/right boundaries)
      let val = 0.2 + normalizedBase * 0.55 + noise * 0.2;
      val = Math.min(0.95, Math.max(0.15, val));
      heights.push(val);
    }
    return heights;
  }, [trackId, duration]);

  // Redraw loop on animation frame for butter-smooth progress alignment
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
      const slotWidth = rect.width / count;
      const widthToDraw = Math.max(1.5, slotWidth * 0.7); // 70% bar width, 30% gap
      const gap = slotWidth - widthToDraw;

      // Disable shadow/glow to keep the bars extremely sharp and crisp
      ctx.shadowColor = "transparent";
      ctx.shadowBlur = 0;

      for (let index = 0; index < count; index += 1) {
        const value = waveformHeights[index] || 0.15;
        const height = Math.max(4, value * rect.height * 0.72); // compact height
        const x = index * slotWidth;
        const y = (rect.height - height) / 2;

        const barLeft = x + gap / 2;
        const barRight = barLeft + widthToDraw;

        if (barRight <= progressX) {
          // Fully elapsed: show active solid white color
          ctx.fillStyle = "rgba(255, 255, 255, 0.95)";
          ctx.fillRect(barLeft, y, widthToDraw, height);
        } else if (barLeft >= progressX) {
          // Fully unplayed: show dim inactive color
          ctx.fillStyle = "rgba(255, 255, 255, 0.24)";
          ctx.fillRect(barLeft, y, widthToDraw, height);
        } else {
          // Partially elapsed transition bar
          const elapsedWidth = progressX - barLeft;
          const remainingWidth = widthToDraw - elapsedWidth;

          // Draw inactive/remaining part
          ctx.fillStyle = "rgba(255, 255, 255, 0.24)";
          ctx.fillRect(barLeft + elapsedWidth, y, remainingWidth, height);

          // Draw active/elapsed part
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
    <button type="button" className={cn("block h-10 w-full cursor-pointer", className)} onPointerDown={seek}>
      <canvas ref={canvasRef} className="h-full w-full" />
    </button>
  );
}
