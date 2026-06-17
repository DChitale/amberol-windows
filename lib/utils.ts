import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatTime(seconds = 0) {
  if (!Number.isFinite(seconds) || seconds < 0) {
    return "0:00";
  }

  const rounded = Math.floor(seconds);
  const minutes = Math.floor(rounded / 60);
  const remainingSeconds = rounded % 60;
  return `${minutes}:${remainingSeconds.toString().padStart(2, "0")}`;
}

export function formatDuration(totalSeconds = 0) {
  if (!Number.isFinite(totalSeconds) || totalSeconds <= 0) {
    return "0 minutes";
  }

  const minutes = Math.round(totalSeconds / 60);
  if (minutes < 60) {
    return `${minutes} minute${minutes === 1 ? "" : "s"}`;
  }

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return `${hours}h ${rest}m`;
}

export function titleForTrack(track: { title: string | null; file_name: string }) {
  return track.title?.trim() || track.file_name;
}

export function artistForTrack(track: { artist: string | null }) {
  return track.artist?.trim() || "Unknown Artist";
}

export function albumForTrack(track: { album: string | null }) {
  return track.album?.trim() || "Unknown Album";
}
