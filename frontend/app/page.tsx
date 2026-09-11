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
        <section className="landing-band landing-hero" aria-labelledby="landing-title">
          <div className="landing-inner landing-inner--hero">
            <h1 id="landing-title">
              Payroll suites got <span className="landing-mark">too large</span>.
            </h1>
            <p className="landing-lede">This product only runs the month.</p>
            <p className="landing-note">
              For a small company in India. Superadmin provisions the account; you sign in and do
              the period.
            </p>
          </div>
        </section>

        <section className="landing-band landing-band--ink" aria-labelledby="landing-contrast-title">
          <div className="landing-inner">
            <p className="landing-kicker">Against the suite</p>
            <h2 id="landing-contrast-title">Hefty software for a monthly job</h2>

            <div className="landing-claims">
              <p className="landing-claim landing-claim--muted">
                HR, leave, tax, and modules nobody opens.
              </p>
              <p className="landing-claim">
                Attendance in. Figures out. Payslips in hand.
              </p>
            </div>
          </div>
        </section>

        <section id="steps" className="landing-band landing-band--steps" aria-labelledby="landing-benefits-title">
          <div className="landing-inner">
            <h2 id="landing-benefits-title">The month, in four steps</h2>
            <ol className="landing-steps">
              <li>
                <h3>People and pay</h3>
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
                <h3>Monthly attendance</h3>
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
                <h3>Calculate, review, lock</h3>
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
                <h3>Payslips</h3>
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
          </div>
        </section>

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
