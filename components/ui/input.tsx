import * as React from "react";
import { cn } from "@/lib/utils";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {}

const Input = React.forwardRef<HTMLInputElement, InputProps>(({ className, type, ...props }, ref) => (
  <input
    type={type}
    className={cn(
      "flex h-10 w-full rounded-md border border-white/10 bg-black/20 px-3 py-2 text-sm text-white outline-none placeholder:text-white/48 focus-visible:ring-2 focus-visible:ring-ring",
      className
    )}
    ref={ref}
    {...props}
  />
));
Input.displayName = "Input";

export { Input };
