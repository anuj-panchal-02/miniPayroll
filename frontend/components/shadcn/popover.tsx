"use client";

import * as PopoverPrimitive from "@radix-ui/react-popover";
import { useState, type ComponentProps } from "react";
import { cn } from "@/lib/cn";

function Popover({ ...props }: ComponentProps<typeof PopoverPrimitive.Root>) {
  return <PopoverPrimitive.Root data-slot="popover" {...props} />;
}

function PopoverTrigger({ ...props }: ComponentProps<typeof PopoverPrimitive.Trigger>) {
  return <PopoverPrimitive.Trigger data-slot="popover-trigger" {...props} />;
}

function PopoverContent({
  className,
  align = "start",
  sideOffset = 4,
  onPlaced,
  style,
  ...props
}: ComponentProps<typeof PopoverPrimitive.Content>) {
  const [placed, setPlaced] = useState(false);

  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        {...props}
        data-slot="popover-content"
        align={align}
        sideOffset={sideOffset}
        onPlaced={() => {
          setPlaced(true);
          onPlaced?.();
        }}
        style={{ ...style, transition: "none", opacity: placed ? 1 : 0 }}
        className={cn(
          "z-50 w-auto rounded-md border border-border bg-popover p-0 text-popover-foreground shadow-sm outline-none",
          className,
        )}
      />
    </PopoverPrimitive.Portal>
  );
}

export { Popover, PopoverTrigger, PopoverContent };
