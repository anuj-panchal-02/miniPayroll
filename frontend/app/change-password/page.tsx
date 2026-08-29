"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { SignOutButton } from "@/components/SignOutButton";
import { BrandLogo } from "@/components/BrandLogo";
import {
  MeResponse,
  changePassword,
  getMe,
  getToken,
  setToken,
} from "@/lib/api";
import { homePath } from "@/lib/setup";
import {
  confirmPasswordError,
  currentPasswordError,
  newPasswordError,
} from "@/lib/validation";
import "@/app/login/login.css";

type Touched = {
  currentPassword: boolean;
  newPassword: boolean;
  confirmPassword: boolean;
};

export default function ChangePasswordPage() {
  const router = useRouter();
  const currentRef = useRef<HTMLInputElement>(null);
  const newRef = useRef<HTMLInputElement>(null);
  const confirmRef = useRef<HTMLInputElement>(null);

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [touched, setTouched] = useState<Touched>({
    currentPassword: false,
    newPassword: false,
    confirmPassword: false,
  });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const [account, setAccount] = useState<MeResponse | null>(null);

  const currentFieldError = currentPasswordError(currentPassword);
  const newFieldError = newPasswordError(newPassword);
  const confirmFieldError = confirmPasswordError(confirmPassword, newPassword);
  const shownCurrentError = touched.currentPassword ? currentFieldError : null;
  const shownNewError = touched.newPassword ? newFieldError : null;
  const shownConfirmError = touched.confirmPassword ? confirmFieldError : null;

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }

    let cancelled = false;
    async function check() {
      try {
        const me = await getMe();
        if (cancelled) {
          return;
        }
        if (!me.mustChangePassword) {
          router.replace(homePath(me));
          return;
        }
        setAccount(me);
        setReady(true);
      } catch (err) {
        if (cancelled) {
          return;
        }
        const message = err instanceof Error ? err.message : "";
        if (
          message.includes("401") ||
          message.toLowerCase().includes("unauthorized")
        ) {
          setToken(null);
          router.replace("/login");
          return;
        }
        setError(err instanceof Error ? err.message : "Could not load account");
        setReady(true);
      }
    }

    void check();
    return () => {
      cancelled = true;
    };
  }, [router]);

  function markTouched(field: keyof Touched) {
    setTouched((current) => ({ ...current, [field]: true }));
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setTouched({
      currentPassword: true,
      newPassword: true,
      confirmPassword: true,
    });
    if (currentFieldError) {
      currentRef.current?.focus();
      return;
    }
    if (newFieldError) {
      newRef.current?.focus();
      return;
    }
    if (confirmFieldError) {
      confirmRef.current?.focus();
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await changePassword(currentPassword, newPassword);
      if (!account) {
        setToken(null);
        router.replace("/login");
        return;
      }
      router.replace(homePath({ ...account, mustChangePassword: false }));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not change password");
      setBusy(false);
    }
  }

  if (!ready) {
    return null;
  }

  return (
    <main className="login">
      <section className="login-intro">
        <BrandLogo size="auth" />
        <h1 id="change-password-title">Change password</h1>
        <p>You must set a new password before continuing.</p>
      </section>

      <div className="login-panel">
        <div className="login-stack">
          <SignOutButton
            onSignOut={() => {
              setToken(null);
              router.push("/login");
            }}
          />
          <form
            className="login-form"
            noValidate
            onSubmit={onSubmit}
            aria-labelledby="change-password-title"
          >
            <div className="login-field">
              <label htmlFor="current-password">Current password</label>
              <input
                ref={currentRef}
                id="current-password"
                name="currentPassword"
                type="password"
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                onBlur={() => markTouched("currentPassword")}
                autoComplete="current-password"
                disabled={busy}
                aria-required="true"
                aria-invalid={shownCurrentError ? true : undefined}
                aria-describedby={
                  shownCurrentError ? "current-password-error" : undefined
                }
              />
              <p id="current-password-error" className="login-field__error" role="alert">
                {shownCurrentError}
              </p>
            </div>
            <div className="login-field">
              <label htmlFor="new-password">New password</label>
              <input
                ref={newRef}
                id="new-password"
                name="newPassword"
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                onBlur={() => markTouched("newPassword")}
                autoComplete="new-password"
                disabled={busy}
                aria-required="true"
                aria-invalid={shownNewError ? true : undefined}
                aria-describedby={shownNewError ? "new-password-error" : undefined}
              />
              <p id="new-password-error" className="login-field__error" role="alert">
                {shownNewError}
              </p>
            </div>
            <div className="login-field">
              <label htmlFor="confirm-password">Confirm password</label>
              <input
                ref={confirmRef}
                id="confirm-password"
                name="confirmPassword"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                onBlur={() => markTouched("confirmPassword")}
                autoComplete="new-password"
                disabled={busy}
                aria-required="true"
                aria-invalid={shownConfirmError ? true : undefined}
                aria-describedby={
                  shownConfirmError ? "confirm-password-error" : undefined
                }
              />
              <p id="confirm-password-error" className="login-field__error" role="alert">
                {shownConfirmError}
              </p>
            </div>

            <p className="login-alert" role="alert">
              {error}
            </p>

            <button
              type="submit"
              className="login-submit"
              disabled={busy}
              aria-busy={busy}
            >
              {busy ? "Saving" : "Change password"}
            </button>
          </form>
        </div>
      </div>
    </main>
  );
}
