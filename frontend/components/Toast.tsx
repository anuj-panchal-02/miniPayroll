"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { CircleAlert, CircleCheck } from "lucide-react";
import { Alert, AlertDescription } from "@/components/shadcn/alert";
import "./Toast.css";

export const TOAST_DURATION_MS = 4000;

export type ToastTone = "error" | "success";

type ToastProps = {
  message: string | null;
  tone?: ToastTone;
  durationMs?: number;
  onDismiss: () => void;
};

export function Toast({
  message,
  tone = "error",
  durationMs = TOAST_DURATION_MS,
  onDismiss,
}: ToastProps) {
  const onDismissRef = useRef(onDismiss);
  onDismissRef.current = onDismiss;

  useEffect(() => {
    if (!message) {
      return;
    }
    const timer = window.setTimeout(() => {
      onDismissRef.current();
    }, durationMs);
    return () => window.clearTimeout(timer);
  }, [message, durationMs]);

  if (!message || typeof document === "undefined") {
    return null;
  }

  const isError = tone === "error";

  return createPortal(
    <div className="mp-toast">
      <Alert variant={isError ? "destructive" : "default"} role={isError ? "alert" : "status"}>
        {isError ? <CircleAlert /> : <CircleCheck />}
        <AlertDescription>{message}</AlertDescription>
      </Alert>
    </div>,
    document.body,
  );
}

export function useToast() {
  const [message, setMessage] = useState<string | null>(null);
  const [tone, setTone] = useState<ToastTone>("error");

  const showError = useCallback((next: string) => {
    setTone("error");
    setMessage(next);
  }, []);

  const showSuccess = useCallback((next: string) => {
    setTone("success");
    setMessage(next);
  }, []);

  const dismiss = useCallback(() => {
    setMessage(null);
  }, []);

  return { message, tone, showError, showSuccess, dismiss };
}

export function ToastOutlet({ toast }: { toast: ReturnType<typeof useToast> }) {
  return <Toast message={toast.message} tone={toast.tone} onDismiss={toast.dismiss} />;
}
