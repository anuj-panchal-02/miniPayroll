import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/cn";

export type BadgeTone = "default" | "neutral" | "success" | "warning" | "error" | "info";
export type BadgeSize = "sm" | "md";

export type BadgeProps = HTMLAttributes<HTMLSpanElement> & {
  tone?: BadgeTone;
  size?: BadgeSize;
  children: ReactNode;
};

export function Badge({
  tone = "default",
  size = "md",
  className,
  children,
  ...props
}: BadgeProps) {
  return (
    <span
      className={cn(
        "mp-badge",
        `mp-badge--${tone}`,
        size === "sm" && "mp-badge--sm",
        className,
      )}
      {...props}
    >
      {children}
    </span>
  );
}
