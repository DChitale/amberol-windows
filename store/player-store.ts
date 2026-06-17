"use client";

import { create } from "zustand";
import type { Playlist, RepeatMode, Track } from "@/types/music";

interface PlayerState {
  tracks: Track[];
  queue: Track[];
  playlists: Playlist[];
  activePlaylistId: number | null;
  currentTrack: Track | null;
  isPlaying: boolean;
  volume: number;
  speed: number;
  shuffle: boolean;
  repeat: RepeatMode;
  search: string;
  recentlyAdded: Track[];
  setTracks: (tracks: Track[]) => void;
  setQueue: (queue: Track[]) => void;
  setPlaylists: (playlists: Playlist[]) => void;
  setActivePlaylistId: (id: number | null) => void;
  setCurrentTrack: (track: Track | null) => void;
  setIsPlaying: (isPlaying: boolean) => void;
  setVolume: (volume: number) => void;
  setSpeed: (speed: number) => void;
  setShuffle: (shuffle: boolean) => void;
  setRepeat: (repeat: RepeatMode) => void;
  toggleRepeat: () => void;
  setSearch: (search: string) => void;
  setRecentlyAdded: (tracks: Track[]) => void;
  nextTrack: () => Track | null;
  previousTrack: () => Track | null;
}

function nextRepeatMode(mode: RepeatMode): RepeatMode {
  if (mode === "off") return "all";
  if (mode === "all") return "one";
  return "off";
}

export const repeatModes: RepeatMode[] = ["off", "all", "one"];

export const usePlayerStore = create<PlayerState>((set, get) => ({
  tracks: [],
  queue: [],
  playlists: [],
  activePlaylistId: null,
  currentTrack: null,
  isPlaying: false,
  volume: 0.72,
  speed: 1,
  shuffle: false,
  repeat: "off",
  search: "",
  recentlyAdded: [],
  setTracks: (tracks) => set({ tracks }),
  setQueue: (queue) => set({ queue, currentTrack: get().currentTrack ?? queue[0] ?? null }),
  setPlaylists: (playlists) => set({ playlists }),
  setActivePlaylistId: (activePlaylistId) => set({ activePlaylistId }),
  setCurrentTrack: (currentTrack) => set({ currentTrack }),
  setIsPlaying: (isPlaying) => set({ isPlaying }),
  setVolume: (volume) => set({ volume }),
  setSpeed: (speed) => set({ speed }),
  setShuffle: (shuffle) => set({ shuffle }),
  setRepeat: (repeat) => set({ repeat }),
  toggleRepeat: () => set({ repeat: nextRepeatMode(get().repeat) }),
  setSearch: (search) => set({ search }),
  setRecentlyAdded: (recentlyAdded) => set({ recentlyAdded }),
  nextTrack: () => {
    const { queue, currentTrack, shuffle, repeat } = get();
    if (!currentTrack || queue.length === 0) {
      return null;
    }

    if (repeat === "one") {
      return currentTrack;
    }

    if (shuffle) {
      const next = queue[Math.floor(Math.random() * queue.length)] ?? currentTrack;
      set({ currentTrack: next });
      return next;
    }

    const currentIndex = queue.findIndex((track) => track.id === currentTrack.id);
    const nextIndex = currentIndex + 1;
    if (nextIndex < queue.length) {
      const next = queue[nextIndex];
      set({ currentTrack: next });
      return next;
    }

    if (repeat === "all") {
      const next = queue[0] ?? null;
      set({ currentTrack: next });
      return next;
    }

    set({ isPlaying: false });
    return null;
  },
  previousTrack: () => {
    const { queue, currentTrack } = get();
    if (!currentTrack || queue.length === 0) {
      return null;
    }

    const currentIndex = queue.findIndex((track) => track.id === currentTrack.id);
    const previous = queue[Math.max(0, currentIndex - 1)] ?? currentTrack;
    set({ currentTrack: previous });
    return previous;
  }
}));
