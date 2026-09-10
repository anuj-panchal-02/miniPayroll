"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { BrandLogo } from "@/components/BrandLogo";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
import { PasswordField } from "@/components/ui/PasswordField";
import { login, setToken } from "@/lib/api";
import { homePath } from "@/lib/setup";
import { emailError } from "@/lib/validation";
import "./login.css";

const EMAIL_MESSAGES = {
  empty: "Enter your email.",
  invalid: "Enter a valid email.",
};

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [emailTouched, setEmailTouched] = useState(false);
  const [passwordTouched, setPasswordTouched] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const emailFieldError = emailError(email, EMAIL_MESSAGES);
  const passwordFieldError = password ? null : "Enter your password.";
  const shownEmailError = emailTouched ? emailFieldError : null;
  const shownPasswordError = passwordTouched ? passwordFieldError : null;

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setEmailTouched(true);
    setPasswordTouched(true);
    if (emailFieldError) {
      document.getElementById("login-email")?.focus();
      return;
    }
    if (passwordFieldError) {
      document.getElementById("login-password")?.focus();
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const result = await login(email, password);
      setToken(result.token);
      router.replace(homePath(result));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sign in failed. Check your email and password.");
      setBusy(false);
    }
  }

  return (
    <main className="login">
      <section className="login-intro">
        <BrandLogo size="auth" />
        <h1 id="login-title">Sign in</h1>
        <p>Payroll for small businesses in India.</p>
      </section>

      <div className="login-panel">
        <form
          className="login-form"
          noValidate
          onSubmit={onSubmit}
          aria-labelledby="login-title"
        >
          <Field
            id="login-email"
            label="Email"
            error={shownEmailError}
            required
          >
            <input
              className="mp-input"
              name="email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              onBlur={() => setEmailTouched(true)}
              autoComplete="username"
              disabled={busy}
            />
          </Field>

          <PasswordField
            id="login-password"
            label="Password"
            name="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            onBlur={() => setPasswordTouched(true)}
            autoComplete="current-password"
            required
            disabled={busy}
            error={shownPasswordError}
          />

          <Alert>{error}</Alert>

          <Button type="submit" loading={busy} loadingLabel="Signing in">
            Sign in
          </Button>
        </form>
      </div>
    </main>
  );
}
