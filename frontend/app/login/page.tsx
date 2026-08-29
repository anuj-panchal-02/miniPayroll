"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { BrandLogo } from "@/components/BrandLogo";
import { login, setToken } from "@/lib/api";
import { homePath } from "@/lib/setup";
import "./login.css";

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("superadmin@minipayroll.local");
  const [password, setPassword] = useState("ChangeMe_Superadmin1!");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const result = await login(email, password);
      setToken(result.token);
      router.replace(homePath(result));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sign in failed");
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
          onSubmit={onSubmit}
          aria-labelledby="login-title"
        >
          <div className="login-field">
            <label htmlFor="login-email">Email</label>
            <input
              id="login-email"
              name="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
              required
              disabled={busy}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? "login-error" : undefined}
            />
          </div>

          <div className="login-field">
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              name="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
              disabled={busy}
              aria-invalid={error ? true : undefined}
            />
          </div>

          <p className="login-alert" id="login-error" role="alert">
            {error}
          </p>

          <button
            type="submit"
            className="login-submit"
            disabled={busy}
            aria-busy={busy}
          >
            {busy ? "Signing in" : "Sign in"}
          </button>
        </form>
      </div>
    </main>
  );
}
