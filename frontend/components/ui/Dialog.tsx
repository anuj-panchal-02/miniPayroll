"use client";

import {
  forwardRef,
  type MouseEvent,
  type ReactNode,
  type Ref,
} from "react";

type DialogProps = {
  title: string;
  description: string;
  titleId: string;
  descriptionId: string;
  children: ReactNode;
  className?: string;
  onBackdropClick?: () => void;
};

export const Dialog = forwardRef(function Dialog(
  { title, description, titleId, descriptionId, children, className, onBackdropClick }: DialogProps,
  ref: Ref<HTMLDialogElement>,
) {
  function handleBackdropClick(event: MouseEvent<HTMLDialogElement>) {
    if (event.target === event.currentTarget) {
      onBackdropClick?.();
    }
  }

  return (
    <dialog
      ref={ref}
      className={["mp-dialog", className].filter(Boolean).join(" ")}
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      onClick={handleBackdropClick}
    >
      <form method="dialog" className="mp-dialog__body">
        <h2 id={titleId} className="mp-dialog__title">
          {title}
        </h2>
        <p id={descriptionId} className="mp-dialog__copy">
          {description}
        </p>
        <div className="mp-dialog__actions">{children}</div>
      </form>
    </dialog>
  );
});
