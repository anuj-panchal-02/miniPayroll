"use client";

import { MouseEvent, useId, useRef, useState } from "react";
import "./SignOutButton.css";

export type SignOutPreviewState =
  | "hover"
  | "focus"
  | "active"
  | "disabled"
  | "loading"
  | "error"
  | "success";

type SignOutButtonProps = {
  onSignOut?: () => void | Promise<void>;
  previewState?: SignOutPreviewState;
};

const PREVIEW_CLASS: Record<SignOutPreviewState, string> = {
  hover: "is-hover",
  focus: "is-focus",
  active: "is-active",
  disabled: "is-disabled",
  loading: "is-loading",
  error: "is-error",
  success: "is-success",
};

export function SignOutButton({ onSignOut, previewState }: SignOutButtonProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const copyId = useId();
  const [status, setStatus] = useState<"idle" | "loading" | "error" | "success">(
    "idle",
  );

  const liveState = previewState ?? status;
  const isDisabled = liveState === "disabled";
  const isBusy = liveState === "loading";
  const dataState =
    liveState === "loading" || liveState === "error" || liveState === "success"
      ? liveState
      : undefined;

  const label =
    liveState === "loading"
      ? "Signing out"
      : liveState === "error"
        ? "Try again"
        : liveState === "success"
          ? "Signed out"
          : "Sign out";

  function openConfirm() {
    dialogRef.current?.showModal();
  }

  function closeConfirm() {
    dialogRef.current?.close();
  }

  async function performSignOut() {
    setStatus("loading");
    try {
      await onSignOut?.();
      setStatus("success");
    } catch {
      setStatus("error");
    }
  }

  function handleTriggerClick() {
    if (previewState || isDisabled || isBusy) {
      return;
    }

    if (status === "error") {
      void performSignOut();
      return;
    }

    openConfirm();
  }

  function handleBackdropClick(event: MouseEvent<HTMLDialogElement>) {
    if (event.target === event.currentTarget) {
      closeConfirm();
    }
  }

  function handleConfirm() {
    closeConfirm();
    void performSignOut();
  }

  return (
    <>
      <button
        type="button"
        className={`sign-out${previewState ? ` ${PREVIEW_CLASS[previewState]}` : ""}`}
        data-state={dataState}
        disabled={isDisabled || isBusy}
        aria-disabled={isDisabled || undefined}
        aria-busy={isBusy || undefined}
        aria-invalid={liveState === "error" || undefined}
        aria-haspopup={previewState ? undefined : "dialog"}
        aria-label={label}
        onClick={handleTriggerClick}
      >
        {liveState === "loading" ? <span className="sign-out__spinner" /> : null}
        {liveState === "error" ? <ErrorMark /> : null}
        {liveState === "success" ? <CheckMark /> : null}
        <span>{label}</span>
      </button>

      {previewState ? null : (
        <dialog
          ref={dialogRef}
          className="sign-out-dialog"
          aria-labelledby={titleId}
          aria-describedby={copyId}
          onClick={handleBackdropClick}
        >
          <form method="dialog" className="sign-out-dialog__body">
            <h2 id={titleId} className="sign-out-dialog__title">
              Sign out
            </h2>
            <p id={copyId} className="sign-out-dialog__copy">
              You will need to sign in again.
            </p>
            <div className="sign-out-dialog__actions">
              <button
                type="button"
                className="sign-out-dialog__confirm"
                onClick={handleConfirm}
              >
                Sign out
              </button>
              <button type="submit" className="sign-out-dialog__cancel">
                Cancel
              </button>
            </div>
          </form>
        </dialog>
      )}
    </>
  );
}

function ErrorMark() {
  return (
    <svg className="sign-out__mark" viewBox="0 0 16 16" aria-hidden="true">
      <path
        fill="currentColor"
        d="M8 1.5a6.5 6.5 0 1 1 0 13 6.5 6.5 0 0 1 0-13Zm0 9.25a.9.9 0 1 0 0 1.8.9.9 0 0 0 0-1.8Zm.8-6.1h-1.6l.18 5h1.24l.18-5Z"
      />
    </svg>
  );
}

function CheckMark() {
  return (
    <svg className="sign-out__mark" viewBox="0 0 16 16" aria-hidden="true">
      <path
        fill="currentColor"
        d="M6.4 11.2 3.2 8l1.15-1.15L6.4 8.9l5.25-5.25L12.8 4.8 6.4 11.2Z"
      />
    </svg>
  );
}
