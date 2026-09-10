import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

export type EmptyStateProps = {
  title: string;
  description?: string;
  action?: ReactNode;
  icon?: ReactNode;
  className?: string;
};

export function EmptyState({
  title,
  description,
  action,
  icon,
  className,
}: EmptyStateProps) {
  return (
    <div className={cn("mp-empty-state", className)} role="status">
      {icon ? <div className="mp-empty-state__icon">{icon}</div> : null}
      <h3 className="mp-empty-state__title">{title}</h3>
      {description ? (
        <p className="mp-empty-state__description">{description}</p>
      ) : null}
      {action ? <div className="mp-empty-state__action">{action}</div> : null}
    </div>
  );
}
