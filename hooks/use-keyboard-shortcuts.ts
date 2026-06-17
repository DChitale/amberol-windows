"use client";

import { useEffect } from "react";

interface ShortcutHandlers {
  togglePlayback: () => void;
  next: () => void;
  previous: () => void;
  seekBy: (seconds: number) => void;
}

export function useKeyboardShortcuts({ togglePlayback, next, previous, seekBy }: ShortcutHandlers) {
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      const isTyping = target?.tagName === "INPUT" || target?.tagName === "TEXTAREA";
      if (isTyping || event.metaKey || event.ctrlKey || event.altKey) {
        return;
      }

      if (event.code === "Space") {
        event.preventDefault();
        togglePlayback();
      }

      if (event.key === "ArrowRight") {
        event.preventDefault();
        seekBy(10);
      }

      if (event.key === "ArrowLeft") {
        event.preventDefault();
        seekBy(-10);
      }

      if (event.key === "ArrowUp") {
        event.preventDefault();
        previous();
      }

      if (event.key === "ArrowDown") {
        event.preventDefault();
        next();
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [next, previous, seekBy, togglePlayback]);
}
