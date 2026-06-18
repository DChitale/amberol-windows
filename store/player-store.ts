"use client";

import { create } from "zustand";
import type { Playlist, RepeatMode, Track } from "@/types/music";

interface PlayerState {
  tracks: Track[];
  queue: Track[];
  originalQueue: Track[];
  playlists: Playlist[];
  activePlaylistId: number | null;
  currentTrack: Track | null;
  isPlaying: boolean;
  volume: number;
  speed: number;
  shuffle: boolean;
  repeat: RepeatMode;
  search: string;
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
  nextTrack: () => Track | null;
  previousTrack: () => Track | null;
}

function nextRepeatMode(mode: RepeatMode): RepeatMode {
  if (mode === "off") return "all";
  if (mode === "all") return "one";
  return "off";
}

export const repeatModes: RepeatMode[] = ["off", "all", "one"];

function shuffleArray(array: Track[], anchorIndex: number): Track[] {
  if (array.length <= 1) return [...array];
  const before = array.slice(0, anchorIndex);
  const anchor = array[anchorIndex];
  const after = array.slice(anchorIndex + 1);

  // Fisher-Yates shuffle algorithm
  const shuffle = (arr: Track[]) => {
    const res = [...arr];
    for (let i = res.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      const temp = res[i];
      if (temp && res[j]) {
        res[i] = res[j]!;
        res[j] = temp;
      }
    }
    return res;
  };

  const shuffledBefore = shuffle(before);
  const shuffledAfter = shuffle(after);

  if (anchor) {
    return [...shuffledBefore, anchor, ...shuffledAfter];
  }
  return [...shuffledBefore, ...shuffledAfter];
}

export const usePlayerStore = create<PlayerState>((set, get) => ({
  tracks: [],
  queue: [],
  originalQueue: [],
  playlists: [],
  activePlaylistId: null,
  currentTrack: null,
  isPlaying: false,
  volume: 0.72,
  speed: 1,
  shuffle: false,
  repeat: "off",
  search: "",
  setTracks: (tracks) => set({ tracks }),
  setQueue: (newQueue) => {
    const { shuffle, currentTrack, originalQueue } = get();
    
    // Check if newQueue is just a subset/reorder of current queue (e.g. reorder or track deletion)
    const currentIds = new Set(get().queue.map((t) => t.id));
    const isSubset = newQueue.length > 0 && newQueue.every((t) => currentIds.has(t.id));

    if (isSubset) {
      if (shuffle) {
        const originalFiltered = originalQueue.filter((t) => newQueue.some((nq) => nq.id === t.id));
        set({
          queue: newQueue,
          originalQueue: originalFiltered,
          currentTrack: currentTrack ?? newQueue[0] ?? null
        });
      } else {
        set({
          queue: newQueue,
          currentTrack: currentTrack ?? newQueue[0] ?? null
        });
      }
    } else {
      // Entirely new queue loading (switching playlist or starting app)
      if (shuffle) {
        const anchorIndex = currentTrack ? newQueue.findIndex((t) => t.id === currentTrack.id) : 0;
        const shuffled = shuffleArray(newQueue, anchorIndex >= 0 ? anchorIndex : 0);
        set({
          originalQueue: newQueue,
          queue: shuffled,
          currentTrack: currentTrack ?? shuffled[0] ?? null
        });
      } else {
        set({
          originalQueue: [],
          queue: newQueue,
          currentTrack: currentTrack ?? newQueue[0] ?? null
        });
      }
    }
  },
  setPlaylists: (playlists) => set({ playlists }),
  setActivePlaylistId: (activePlaylistId) => set({ activePlaylistId }),
  setCurrentTrack: (currentTrack) => set({ currentTrack }),
  setIsPlaying: (isPlaying) => set({ isPlaying }),
  setVolume: (volume) => set({ volume }),
  setSpeed: (speed) => set({ speed }),
  setShuffle: (shuffle) => {
    const { queue, currentTrack, originalQueue } = get();
    if (shuffle) {
      if (get().shuffle) return;
      const anchorIndex = currentTrack ? queue.findIndex((t) => t.id === currentTrack.id) : 0;
      const shuffled = shuffleArray(queue, anchorIndex >= 0 ? anchorIndex : 0);
      set({
        originalQueue: queue,
        queue: shuffled,
        shuffle: true
      });
    } else {
      if (!get().shuffle) return;
      set({
        queue: originalQueue.length ? originalQueue : queue,
        originalQueue: [],
        shuffle: false
      });
    }
  },
  setRepeat: (repeat) => set({ repeat }),
  toggleRepeat: () => set({ repeat: nextRepeatMode(get().repeat) }),
  setSearch: (search) => set({ search }),
  nextTrack: () => {
    const { queue, currentTrack, repeat } = get();
    if (!currentTrack || queue.length === 0) {
      return null;
    }


    const currentIndex = queue.findIndex((track) => track.id === currentTrack.id);
    const nextIndex = currentIndex + 1;
    if (nextIndex < queue.length) {
      const next = queue[nextIndex] ?? null;
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
    const { queue, currentTrack, repeat } = get();
    if (!currentTrack || queue.length === 0) {
      return null;
    }

    const currentIndex = queue.findIndex((track) => track.id === currentTrack.id);
    if (currentIndex > 0) {
      const prev = queue[currentIndex - 1] ?? null;
      set({ currentTrack: prev });
      return prev;
    }

    if (repeat === "all") {
      const prev = queue[queue.length - 1] ?? null;
      set({ currentTrack: prev });
      return prev;
    }

    return currentTrack;
  }
}));
