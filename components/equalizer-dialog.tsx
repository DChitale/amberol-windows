"use client";

import { useEffect, useState } from "react";
import { invoke } from "@tauri-apps/api/core";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { cn } from "@/lib/utils";

// 10 EQ Bands matching the .fac files
export const EQ_BANDS = [
  "62.5Hz",
  "110Hz",
  "250Hz",
  "370Hz",
  "650Hz",
  "1.2kHz",
  "2.13kHz",
  "4.55kHz",
  "6.85kHz",
  "16kHz"
];

const DEFAULT_PRESETS: Record<string, number[]> = {
  "Flat": [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
  "Bass": [5.5, 4.0, 3.0, 1.5, 0, 0, 0, -1.0, -2.0, -2.5],
  "Vocal": [-2.0, -1.0, 1.0, 2.5, 4.5, 4.0, 3.0, 2.0, 1.0, 1.0],
  "Pop": [-1.0, 0.5, 2.0, 3.0, 4.0, 3.5, 2.0, 1.0, -1.0, -2.0],
  "Classical": [3.0, 2.5, 2.0, 1.0, 0, 0, 1.5, 2.5, 3.0, 3.5],
  "Rock": [4.5, 3.5, 2.5, 1.0, -1.0, 0, 1.5, 2.5, 3.5, 4.0]
};

interface EqualizerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  enabled: boolean;
  onEnabledChange: (enabled: boolean) => void;
  gains: number[];
  onGainsChange: (gains: number[]) => void;
  activePreset: string;
  onPresetChange: (preset: string) => void;
}

export function EqualizerDialog({
  open,
  onOpenChange,
  enabled,
  onEnabledChange,
  gains,
  onGainsChange,
  activePreset,
  onPresetChange
}: EqualizerDialogProps) {
  const [presets, setPresets] = useState<Record<string, number[]>>(DEFAULT_PRESETS);

  // Load presets dynamically from Rust backend on mount
  useEffect(() => {
    const loadDynamicPresets = async () => {
      try {
        const loaded: { name: string; gains: number[] }[] = await invoke("get_equalizer_presets");
        if (loaded && loaded.length > 0) {
          const map: Record<string, number[]> = { ...DEFAULT_PRESETS };
          loaded.forEach((preset) => {
            map[preset.name] = preset.gains;
          });
          setPresets(map);
        }
      } catch (err) {
        console.error("Failed to load equalizer presets from backend:", err);
      }
    };
    
    if (open) {
      void loadDynamicPresets();
    }
  }, [open]);

  const handleGainChange = (index: number, val: number) => {
    const nextGains = [...gains];
    nextGains[index] = val;
    onGainsChange(nextGains);
    onPresetChange("custom");
  };

  const handleSelectPreset = (key: string) => {
    onPresetChange(key);
    if (key !== "custom") {
      const presetGains = presets[key];
      if (presetGains) {
        onGainsChange([...presetGains]);
      }
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(92vw,400px)]">
        <DialogHeader className="flex flex-row items-center justify-between pr-8 mb-2">
          <DialogTitle className="text-base font-bold">Equalizer</DialogTitle>
          <Button
            variant="ghost"
            onClick={() => onEnabledChange(!enabled)}
            className={cn(
              "text-[10px] font-bold uppercase tracking-wider px-2.5 py-1.5 h-auto rounded-full border transition-all no-drag",
              enabled
                ? "bg-white/20 border-white/12 text-white hover:bg-white/28"
                : "bg-transparent border-transparent text-white/50 hover:bg-white/8 hover:text-white"
            )}
          >
            {enabled ? "Active" : "OFF"}
          </Button>
        </DialogHeader>

        {/* Sliders Container (10 bands) */}
        <div
          className={cn(
            "flex justify-start items-end h-[176px] py-4 pb-3 px-3 bg-black/16 rounded-xl border border-white/5 no-drag transition-opacity duration-300 overflow-x-auto eq-scrollbar gap-2",
            !enabled && "opacity-45 pointer-events-none"
          )}
        >
          {EQ_BANDS.map((label, idx) => {
            const gain = gains[idx] ?? 0;
            return (
              <div key={label} className="flex flex-col items-center gap-1.5 h-full justify-between flex-shrink-0 w-10">
                <span className="text-[8px] tabular-nums font-bold text-white/50 text-center truncate w-full">
                  {gain > 0 ? `+${gain.toFixed(0)}` : gain.toFixed(0)}
                </span>
                
                {/* Vertical Slider */}
                <div className="flex-1 flex items-center justify-center py-1">
                  <input
                    type="range"
                    min="-12"
                    max="12"
                    step="0.5"
                    value={gain}
                    onChange={(e) => handleGainChange(idx, Number(e.target.value))}
                    disabled={!enabled}
                    style={{
                      WebkitAppearance: "slider-vertical",
                      height: "90px",
                      width: "8px",
                      background: "transparent",
                      cursor: "pointer"
                    }}
                    className="accent-white cursor-pointer opacity-80 hover:opacity-100 transition-opacity"
                  />
                </div>

                <div className="flex flex-col items-center w-full min-w-0 leading-none">
                  <span className="text-[8px] font-bold text-white/80 truncate w-full text-center">{label}</span>
                </div>
              </div>
            );
          })}
        </div>

        {/* Presets Grid */}
        <div className="flex flex-col gap-2 mt-2">
          <div className="text-[9px] font-bold uppercase tracking-wider text-white/40 px-1">Presets</div>
          <div className="grid grid-cols-3 gap-1.5 no-drag max-h-[140px] overflow-y-auto pr-1">
            {Object.keys(presets).map((key) => (
              <Button
                key={key}
                variant="ghost"
                disabled={!enabled}
                onClick={() => handleSelectPreset(key)}
                className={cn(
                  "text-[10px] font-semibold py-2 h-auto rounded-lg border transition-all truncate text-left justify-start px-2",
                  activePreset === key && enabled
                    ? "bg-white/14 border-white/10 text-white"
                    : "bg-transparent border-transparent text-white/50 hover:bg-white/8 hover:text-white"
                )}
              >
                {key}
              </Button>
            ))}
            <Button
              variant="ghost"
              disabled={!enabled || activePreset !== "custom"}
              onClick={() => handleSelectPreset("custom")}
              className={cn(
                "text-[10px] font-semibold py-2 h-auto rounded-lg border capitalize transition-all justify-start px-2",
                activePreset === "custom" && enabled
                  ? "bg-white/14 border-white/10 text-white"
                  : "bg-transparent border-transparent text-white/30 cursor-default"
              )}
            >
              Custom
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
