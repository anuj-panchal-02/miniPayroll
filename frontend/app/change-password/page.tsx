"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { SignOutButton } from "@/components/SignOutButton";
import { BrandLogo } from "@/components/BrandLogo";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { PasswordField } from "@/components/ui/PasswordField";
import {
  MeResponse,
  changePassword,
  getMe,
  getToken,
  setToken,
} from "@/lib/api";
import { homePath } from "@/lib/setup";
import {
  PASSWORD_RULE_MESSAGE,
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
            <PasswordField
              id="current-password"
              label="Current password"
              name="currentPassword"
              inputRef={currentRef}
              value={currentPassword}
              onChange={(event) => setCurrentPassword(event.target.value)}
              onBlur={() => markTouched("currentPassword")}
              autoComplete="current-password"
              disabled={busy}
              required
              error={shownCurrentError}
            />
            <PasswordField
              id="new-password"
              label="New password"
              name="newPassword"
              inputRef={newRef}
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              onBlur={() => markTouched("newPassword")}
              autoComplete="new-password"
              disabled={busy}
              required
              hint={PASSWORD_RULE_MESSAGE}
              error={shownNewError}
            />
            <PasswordField
              id="confirm-password"
              label="Confirm password"
              name="confirmPassword"
              inputRef={confirmRef}
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              onBlur={() => markTouched("confirmPassword")}
              autoComplete="new-password"
              disabled={busy}
              required
              error={shownConfirmError}
            />

            <Alert>{error}</Alert>

            <Button type="submit" loading={busy} loadingLabel="Saving">
              Change password
            </Button>
          </form>
        </div>
      </div>
    </main>
  );
}
