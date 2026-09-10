"use client";

import { useEffect, useId, useRef } from "react";
import { Button } from "@/components/ui/Button";

type ConfirmDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  confirmLabel: string;
  tone?: "default" | "destructive";
  onConfirm: () => void;
  confirmLoading?: boolean;
  confirmLoadingLabel?: string;
};

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  tone = "default",
  onConfirm,
  confirmLoading,
  confirmLoadingLabel,
}: ConfirmDialogProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const el = ref.current;
    if (!el) {
      return;
    }
    function handleClose() {
      onOpenChange(false);
    }
    el.addEventListener("close", handleClose);
    return () => el.removeEventListener("close", handleClose);
  }, [onOpenChange]);

  useEffect(() => {
    const el = ref.current;
    if (!el) {
      return;
    }
    if (open) {
      if (typeof el.showModal === "function") {
        try {
          if (!el.open) {
            el.showModal();
          }
        } catch {
          el.setAttribute("open", "");
        }
      } else {
        el.setAttribute("open", "");
      }
    } else if (el.open && typeof el.close === "function") {
      try {
        el.close();
      } catch {
        el.removeAttribute("open");
      }
    } else {
      el.removeAttribute("open");
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      className="mp-dialog sa-dialog"
      role="alertdialog"
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onOpenChange(false);
        }
      }}
    >
      <form method="dialog" className="mp-dialog__body">
        <h2 id={titleId} className="mp-dialog__title">
          {title}
        </h2>
        <p id={descriptionId} className="mp-dialog__copy">
          {description}
        </p>
        <div className="mp-dialog__actions">
          <Button
            type="button"
            variant={tone === "destructive" ? "destructive" : "primary"}
            className="sa-dialog__confirm"
            loading={confirmLoading}
            loadingLabel={confirmLoadingLabel}
            onClick={onConfirm}
          >
            {confirmLabel}
          </Button>
          <button type="submit" className="sa-dialog__cancel">
            Cancel
          </button>
        </div>
      </form>
    </dialog>
  );
}
