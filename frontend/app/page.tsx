"use client";

import Link from "next/link";
import { BrandLogo } from "@/components/BrandLogo";
import { useState, useEffect } from "react";
import "./landing.css";

// Abstract UI components
const MiniPayslip = () => (
  <div className="ui-mock ui-mock--payslip">
    <div className="ui-mock__header"></div>
    <div className="ui-mock__row"></div>
    <div className="ui-mock__row"></div>
    <div className="ui-mock__total"></div>
  </div>
);

const MiniTable = () => (
  <div className="ui-mock ui-mock--table">
    <div className="ui-mock__thead"></div>
    <div className="ui-mock__trow"><div className="ui-mock__cell"></div><div className="ui-mock__cell-badge"></div></div>
    <div className="ui-mock__trow"><div className="ui-mock__cell"></div><div className="ui-mock__cell-badge"></div></div>
    <div className="ui-mock__trow"><div className="ui-mock__cell"></div><div className="ui-mock__cell-badge"></div></div>
  </div>
);

const MiniCard = () => (
  <div className="ui-mock ui-mock--card">
    <div className="ui-mock__avatar"></div>
    <div className="ui-mock__lines">
      <div className="ui-mock__line"></div>
      <div className="ui-mock__line short"></div>
    </div>
  </div>
);

const stepsData = [
  {
    id: "people",
    title: "People and pay",
    desc: "Keep salaried employees and dated salary structures.",
    ui: <MiniCard />
  },
  {
    id: "attendance",
    title: "Monthly attendance",
    desc: "Enter working days, present days, and leave before calculating.",
    ui: <MiniTable />
  },
  {
    id: "calculate",
    title: "Calculate, review, lock",
    desc: "Draft the run, check gross and net, then finalize figures.",
    ui: <MiniTable />
  },
  {
    id: "payslips",
    title: "Payslips",
    desc: "Download slips after the run is closed.",
    ui: <MiniPayslip />
  }
];

export default function Home() {
  const [activeStep, setActiveStep] = useState(0);
  const [isScrolled, setIsScrolled] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 20);
    };
    window.addEventListener("scroll", handleScroll);
    return () => window.removeEventListener("scroll", handleScroll);
  }, []);

  return (
    <div className="landing">
      <a className="landing-skip" href="#landing-main">
        Skip to content
      </a>

      <nav className={`landing-nav ${isScrolled ? 'landing-nav--scrolled' : ''}`} aria-label="Primary">
        <Link href="/" className="landing-nav__brand">
          <BrandLogo size="nav" />
        </Link>
        <Link href="/login" className="mp-btn mp-btn--primary landing-nav__login">
          Log in
        </Link>
      </nav>

      <main id="landing-main" className="landing-main">
        {/* HERO SECTION */}
        <section className="landing-band landing-hero fade-in-up" aria-labelledby="landing-title">
          <div className="landing-hero__bg">
             <div className="hero-glow"></div>
             <div className="hero-glow hero-glow--2"></div>
          </div>
          <div className="landing-inner landing-inner--hero">
            <h1 id="landing-title">
              Payroll suites got <span className="landing-mark">too large</span>.
            </h1>
            <p className="landing-lede">This product only runs the month.</p>
            <p className="landing-note">
              For any business in India. Superadmin provisions the account; you sign in and do the period.
            </p>
            <div className="landing-hero-actions">
              <Link href="/login" className="mp-btn mp-btn--primary mp-btn--lg btn-glow">
                Start calculating
              </Link>
            </div>
          </div>
        </section>

        {/* FEATURES GRID SECTION */}
        <section className="landing-band landing-band--features fade-in-up-delay" aria-labelledby="landing-features-title">
          <div className="landing-inner">
            <div className="section-header">
               <h2 id="landing-features-title" className="section-title">Everything you need, nothing you don't.</h2>
               <p className="section-subtitle">We stripped away the HR bloat to give you a tool that just works.</p>
            </div>
            <div className="features-grid">
               <div className="feature-card glass-panel">
                  <div className="feature-card__icon">
                     <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20 6L9 17l-5-5"/></svg>
                  </div>
                  <h3>No HR Bloat</h3>
                  <p>We removed performance reviews, OKRs, and surveys. Just payroll.</p>
               </div>
               <div className="feature-card glass-panel">
                  <div className="feature-card__icon">
                     <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
                  </div>
                  <h3>Lightning Fast</h3>
                  <p>Calculate payroll for your entire team in minutes, not hours.</p>
               </div>
               <div className="feature-card glass-panel">
                  <div className="feature-card__icon">
                     <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
                  </div>
                  <h3>Secure & Locked</h3>
                  <p>Once a month is finalized, figures cannot drift or change.</p>
               </div>
               <div className="feature-card glass-panel">
                  <div className="feature-card__icon">
                     <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>
                  </div>
                  <h3>Instant Payslips</h3>
                  <p>Beautiful, compliant payslips generated automatically.</p>
               </div>
            </div>
          </div>
        </section>

        {/* WALKTHROUGH SECTION */}
        <section id="steps" className="landing-band landing-band--steps fade-in-up-delay-2" aria-labelledby="landing-benefits-title">
          <div className="landing-inner">
            <div className="section-header">
               <h2 id="landing-benefits-title" className="section-title">The month, in four steps</h2>
               <p className="section-subtitle">A straightforward workflow designed for simplicity.</p>
            </div>
            
            <div className="landing-showcase">
              <div className="landing-showcase__tabs">
                {stepsData.map((step, index) => (
                  <button 
                    key={step.id}
                    className={`landing-showcase__tab ${activeStep === index ? 'is-active' : ''}`}
                    onClick={() => setActiveStep(index)}
                  >
                    <span className="landing-showcase__tab-num">{index + 1}</span>
                    <div className="landing-showcase__tab-content">
                      <h3>{step.title}</h3>
                      <p>{step.desc}</p>
                    </div>
                  </button>
                ))}
              </div>
              
              <div className="landing-showcase__display glass-panel">
                {stepsData.map((step, index) => (
                  <div 
                    key={step.id} 
                    className={`landing-showcase__panel ${activeStep === index ? 'is-active' : ''}`}
                    aria-hidden={activeStep !== index}
                  >
                    <div className="mock-container">
                       {step.ui}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* PRICING SECTION */}
        <section className="landing-band landing-band--pricing fade-in-up" aria-labelledby="landing-pricing-title">
          <div className="landing-inner landing-inner--center">
             <div className="pricing-tag">Simple Pricing</div>
             <h2 id="landing-pricing-title" className="section-title">Pay per employee. Zero surprises.</h2>
             <p className="landing-note max-w-lg mx-auto">Because businesses shouldn't pay taxes on essential software.</p>
             
             <div className="pricing-card glass-panel">
                <div className="pricing-header">
                   <h3>Pro Plan</h3>
                   <div className="price"><span>₹49</span> / employee / month</div>
                   <p>For growing teams.</p>
                </div>
                <ul className="pricing-features">
                   <li><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20 6L9 17l-5-5"/></svg> Unlimited payroll runs</li>
                   <li><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20 6L9 17l-5-5"/></svg> Automated payslip generation</li>
                   <li><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20 6L9 17l-5-5"/></svg> Salary structures & deductions</li>
                   <li><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20 6L9 17l-5-5"/></svg> Data locked & secured forever</li>
                   <li><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20 6L9 17l-5-5"/></svg> No feature gating</li>
                </ul>
                <Link href="/login" className="mp-btn mp-btn--primary mp-btn--lg pricing-btn">
                  Get Started
                </Link>
             </div>
          </div>
        </section>

        {/* CTA SECTION */}
        <section className="landing-band landing-band--wash fade-in-up" aria-labelledby="landing-cta-title">
          <div className="landing-inner landing-inner--center">
            <h2 id="landing-cta-title" className="section-title">Already on the company?</h2>
            <p className="landing-note">Existing users sign in with the account Superadmin created.</p>
            <Link href="/login" className="mp-btn mp-btn--primary mp-btn--lg">
              Log in to Dashboard
            </Link>
          </div>
        </section>
      </main>

      <footer className="landing-foot">
        <div className="landing-foot__meta">
          <BrandLogo size="nav" />
          <div className="landing-foot__details">
             <p>miniPayroll · India</p>
             <p className="landing-foot__tagline">Payroll without the suite.</p>
          </div>
        </div>
      </footer>
    </div>
  );
}
