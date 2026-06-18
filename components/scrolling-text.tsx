"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/utils";

interface ScrollingTextProps {
  text: string;
  className?: string;
}

export function ScrollingText({ text, className }: ScrollingTextProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const textRef = useRef<HTMLSpanElement>(null);
  const [shouldScroll, setShouldScroll] = useState(false);

  useEffect(() => {
    const checkOverflow = () => {
      const container = containerRef.current;
      const textEl = textRef.current;
      if (container && textEl) {
        // Measure the un-truncated width of the text against the container's width
        const isOverflowing = textEl.offsetWidth > container.clientWidth;
        setShouldScroll(isOverflowing);
      }
    };

    // Run the initial measurement
    checkOverflow();

    // Set up listeners for resize and a short timeout to handle layout settles
    window.addEventListener("resize", checkOverflow);
    const timer = setTimeout(checkOverflow, 150);

    return () => {
      window.removeEventListener("resize", checkOverflow);
      clearTimeout(timer);
    };
  }, [text]);

  return (
    <div 
      ref={containerRef} 
      className="w-full overflow-hidden relative select-none flex justify-center"
    >
      <div
        className={cn(
          "inline-flex whitespace-nowrap",
          shouldScroll ? "animate-marquee justify-start w-max" : "justify-center w-full"
        )}
        style={{ 
          animationDuration: shouldScroll ? `${Math.max(8, text.length * 0.35)}s` : undefined 
        }}
      >
        <span 
          ref={textRef} 
          className={cn(className, shouldScroll && "pr-12 shrink-0")}
        >
          {text}
        </span>
        {shouldScroll && (
          <span 
            className={cn(className, "pr-12 shrink-0")}
            aria-hidden="true"
          >
            {text}
          </span>
        )}
      </div>
    </div>
  );
}
