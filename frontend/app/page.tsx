import Link from "next/link";
import { BrandLogo } from "@/components/BrandLogo";
import "./landing.css";

function Shot({
  src,
  alt,
  width,
  height,
  caption,
  className,
}: {
  src: string;
  alt: string;
  width: number;
  height: number;
  caption?: string;
  className?: string;
}) {
  return (
    <figure className={["landing-shot", className].filter(Boolean).join(" ")}>
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={src} alt={alt} width={width} height={height} />
      {caption ? <figcaption>{caption}</figcaption> : null}
    </figure>
  );
}

export default function Home() {
  return (
    <div className="landing">
      <a className="landing-skip" href="#landing-main">
        Skip to content
      </a>

      <nav className="landing-nav" aria-label="Primary">
        <Link href="/" className="landing-nav__brand">
          <BrandLogo size="nav" />
        </Link>
        <Link href="/login" className="mp-btn mp-btn--primary">
          Log in
        </Link>
      </nav>

      <main id="landing-main" className="landing-main">
        {/* Hero Section */}
        <section className="landing-band landing-hero" aria-labelledby="landing-title">
          <div className="landing-inner landing-inner--hero">
            <div className="landing-tag">
              <span>🇮🇳</span>
              <span>Payroll for India · 1–50 Salaried Employees</span>
            </div>

            <h1 id="landing-title">
              Payroll suites got <span className="landing-mark">too large</span>.
            </h1>
            <p className="landing-lede">This product only runs the month.</p>
            <p className="landing-note">
              For a small company in India. Superadmin provisions the account; you sign in and do the period.
            </p>

            <div className="landing-hero__actions">
              <Link href="/login" className="mp-btn mp-btn--primary">
                Sign in to workspace →
              </Link>
              <a href="#steps" className="mp-btn mp-btn--secondary">
                See how it works ↓
              </a>
            </div>
          </div>
        </section>

        {/* Contrast Band */}
        <section className="landing-band landing-band--ink" aria-labelledby="landing-contrast-title">
          <div className="landing-inner">
            <p className="landing-kicker">Against the suite</p>
            <h2 id="landing-contrast-title">Hefty software for a monthly job</h2>

            <div className="landing-claims">
              <p className="landing-claim">HR, leave, tax, and modules nobody opens.</p>
              <p className="landing-claim">Attendance in. Figures out. Payslips in hand.</p>
            </div>

            <div className="landing-comparison-grid">
              <div className="landing-contrast-card">
                <h3>The Bloated Suite</h3>
                <ul>
                  <li>✕ 40 enterprise modules you will never touch</li>
                  <li>✕ Complex onboarding and multi-tier approval mazes</li>
                  <li>✕ Expensive per-user licensing and unpredictable pricing</li>
                  <li>✕ Spreadsheets still needed to verify what actually happened</li>
                </ul>
              </div>
              <div className="landing-contrast-card landing-contrast-card--highlight">
                <h3>miniPayroll</h3>
                <ul>
                  <li>✓ Attendance in. Figures out. Payslips in hand.</li>
                  <li>✓ Straightforward 1–50 employee capacity</li>
                  <li>✓ Strict attendance identity and deterministic proration</li>
                  <li>✓ Clean QuestPDF payslips with Indian Rupee in words</li>
                </ul>
              </div>
            </div>
          </div>
        </section>

        {/* 4-Step Narrative Workflow */}
        <section id="steps" className="landing-band landing-band--steps" aria-labelledby="landing-benefits-title">
          <div className="landing-inner">
            <h2 id="landing-benefits-title">The month, in four steps</h2>
            <ol className="landing-steps">
              <li>
                <div className="landing-step__head">
                  <span className="landing-step__badge">01</span>
                  <h3>People and pay</h3>
                </div>
                <p>
                  Keep salaried employees and dated salary structures — one Basic Salary line, with
                  the rest of the month built on that.
                </p>
                <Shot
                  src="/landing/add-employee.png"
                  width={1024}
                  height={670}
                  alt="Add employee wizard, step 1 of 4, showing personal details fields."
                  caption="Onboarding is a four-step form. This is personal details."
                />
              </li>

              <li>
                <div className="landing-step__head">
                  <span className="landing-step__badge">02</span>
                  <h3>Monthly attendance</h3>
                </div>
                <p>
                  Enter working days, present days, and leave for the period before you calculate.
                </p>
                <div className="landing-shot-stack">
                  <Shot
                    src="/landing/monthly-inputs.png"
                    width={1024}
                    height={340}
                    alt="Monthly inputs table with working days, present days, and leave for one employee."
                    caption="Attendance sits on one row per person."
                  />
                  <Shot
                    src="/landing/monthly-inputs-extras.png"
                    width={1024}
                    height={413}
                    alt="The same monthly inputs row with overtime, bonus, and deduction extras expanded."
                    caption="Overtime, bonus, and deductions stay on that row."
                  />
                </div>
              </li>

              <li>
                <div className="landing-step__head">
                  <span className="landing-step__badge">03</span>
                  <h3>Calculate, review, lock</h3>
                </div>
                <p>
                  Draft the run, check gross and net, then finalize so the figures cannot drift.
                </p>
                <Shot
                  src="/landing/review.png"
                  width={1024}
                  height={434}
                  alt="Payroll review for a finalized month: locked figures, payslip download, and payment fields for one employee."
                  caption="A finalized month. Figures are locked."
                />
              </li>

              <li>
                <div className="landing-step__head">
                  <span className="landing-step__badge">04</span>
                  <h3>Payslips</h3>
                </div>
                <p>
                  Download slips after the run is closed. Payment against the month stays on the
                  locked figures.
                </p>
                <Shot
                  src="/landing/history.png"
                  width={1024}
                  height={340}
                  alt="Payroll history listing calculated, reversed, and finalized months with employee count and net pay."
                  caption="Closed months stay on the ledger."
                />
              </li>
            </ol>

            {/* Indian Payroll Guarantees Grid */}
            <div className="landing-feature-grid">
              <div className="landing-feature-card">
                <h4>Indian Rupee In-Words</h4>
                <p>Net payouts are automatically converted to formal Indian notation (Lakhs and Crores) on generated payslips.</p>
              </div>
              <div className="landing-feature-card">
                <h4>Statutory Deductions</h4>
                <p>Dedicated line-item slots for PF, ESI, Professional Tax, TDS, and Labour Welfare Fund (LWF).</p>
              </div>
              <div className="landing-feature-card">
                <h4>Encrypted Financial Data</h4>
                <p>Bank account numbers and IFSC codes are protected with AES encryption at rest.</p>
              </div>
              <div className="landing-feature-card">
                <h4>Immutable Snapshots</h4>
                <p>Once a run is finalized, figures are locked forever. Live edits to employees never alter historical records.</p>
              </div>
            </div>
          </div>
        </section>

        {/* CTA Section */}
        <section className="landing-band landing-band--wash" aria-labelledby="landing-cta-title">
          <div className="landing-inner">
            <h2 id="landing-cta-title">Already on the company?</h2>
            <p className="landing-note">Existing users sign in with the account Superadmin created.</p>
            <Link href="/login" className="mp-btn mp-btn--primary">
              Log in
            </Link>
          </div>
        </section>
      </main>

      {/* Footer */}
      <footer className="landing-foot">
        <p className="landing-foot__line">Payroll without the suite.</p>
        <div className="landing-foot__meta">
          <BrandLogo size="nav" />
          <p>miniPayroll · India · 1–50 salaried employees</p>
        </div>
      </footer>
    </div>
  );
}
