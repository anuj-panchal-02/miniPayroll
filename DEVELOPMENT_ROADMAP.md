# miniPayroll SaaS Development Roadmap

Last updated: 2026-08-29  
Overall status: Foundation / pre-payroll MVP  
Estimated PRD completion: 30–35%

## How to use this document

- Check an item only after implementation, automated tests, and documentation are complete.
- Complete phases in order unless an item explicitly says it can run in parallel.
- `P0` blocks production or the core payroll MVP.
- `P1` is required before onboarding paying customers.
- `P2` can follow the initial controlled launch.
- Add links to issues or pull requests beside each completed item.

## Current foundation

Already implemented:

- [x] ASP.NET Core API, Next.js frontend, and SQL Server persistence
- [x] ASP.NET Identity authentication with JWT
- [x] Superadmin company creation and activation
- [x] Company Admin onboarding and forced password change
- [x] Company setup wizard
- [x] Tenant filters on primary tenant-owned entities
- [x] Employee CRUD with draft and active states
- [x] Employee-limit enforcement
- [x] Encrypted bank account and IFSC storage
- [x] Effective-dated salary structures
- [x] Backend and frontend unit tests for existing features

---

## Phase 0 — Product decisions and specification alignment

Priority: P0  
Goal: Remove contradictions before building payroll and billing rules.

- [ ] Decide whether the platform employee cap is 20 or 50.
- [ ] Set the default company employee limit to the agreed value (PRD currently says 9).
- [ ] Confirm pricing: per active employee, minimum charge, taxes, and rounding.
- [ ] Confirm subscription states and allowed behavior for each:
  - Pending
  - Active
  - Past Due
  - Suspended
  - Cancelled
- [ ] Define payroll periods, cutoff rules, and supported pay frequencies.
- [ ] Confirm attendance identity and unpaid-leave calculation rules.
- [ ] Confirm salary revision behavior when effective dates fall mid-period.
- [ ] Confirm rounding rules for components, gross pay, deductions, and net pay.
- [ ] Confirm payroll reversal policy and required reason/audit details.
- [ ] Define the initial statutory scope for PF, ESI, PT, LWF, and TDS.
- [ ] Align the PRD, README, backend constants, frontend constants, and tests.

Exit criteria:

- [ ] A single approved set of payroll, employee-limit, and billing rules exists.
- [ ] No known PRD-to-code contradictions remain for the MVP scope.

---

## Phase 1 — Security and tenant-isolation hardening

Priority: P0  
Goal: Make the foundation safe before adding real payroll or customer data.

### Secrets and credentials

- [ ] Remove the JWT signing key and Superadmin password from tracked configuration.
- [ ] Load secrets from environment variables or a managed secret store.
- [ ] Require an explicit, secure production bootstrap process for the first Superadmin.
- [ ] Remove default credentials from the production frontend build.
- [ ] Stop returning temporary passwords in API responses.
- [ ] Stop writing temporary passwords to application logs.
- [ ] Integrate secure one-time account invitation delivery.

### Authentication and sessions

- [ ] Replace long-lived JWTs in `localStorage` with HttpOnly, Secure, SameSite sessions or a BFF design.
- [ ] Add short-lived access sessions and rotating refresh tokens if JWTs remain.
- [ ] Revoke active sessions after password changes, credential resets, suspension, or account disablement.
- [ ] Implement Superadmin MFA enrollment, verification, recovery codes, and reset controls.
- [ ] Add login rate limiting by IP and account identifier.
- [ ] Add password-reset and forgotten-password flows.
- [ ] Add security-event auditing for login, lockout, password, MFA, and session events.

### Tenant and financial-data safety

- [ ] Add tenant protection for `EmployeeSalaryComponent`.
- [ ] Remove the production fallback that treats a missing tenant context as Superadmin.
- [ ] Add cross-tenant authorization tests for every tenant-owned endpoint.
- [ ] Return masked bank details by default.
- [ ] Require explicit authorization and an audit event to reveal full bank details.
- [ ] Protect company uploads with authorization or short-lived signed URLs.
- [ ] Verify that logs, errors, analytics, and traces never contain financial PII.

### Web and API hardening

- [ ] Add centralized exception handling with safe Problem Details responses.
- [ ] Add HSTS and production security headers, including CSP and frame protection.
- [ ] Restrict `AllowedHosts` and production CORS origins.
- [ ] Enable request-model validation for all minimal API endpoints.
- [ ] Add request-size and abuse limits to sensitive endpoints.

Exit criteria:

- [ ] No credentials or signing secrets are stored in the repository.
- [ ] Tenant-isolation tests cover all tenant-owned data.
- [ ] Superadmin MFA and secure account recovery work end to end.
- [ ] A stolen browser script cannot directly read reusable authentication tokens.

---

## Phase 2 — Production engineering foundation

Priority: P0  
Goal: Establish safe, repeatable delivery and recovery before expanding the product.

### Configuration and deployment

- [ ] Move the machine-specific SQL Express connection to local user secrets.
- [ ] Document every required environment variable.
- [ ] Add an `.env.example` containing placeholders only.
- [ ] Add reproducible API and frontend production builds.
- [ ] Add Dockerfiles and a local production-like composition, if containers are the deployment target.
- [ ] Select the hosting platform and document the architecture.
- [ ] Configure HTTPS, domains, certificates, and reverse-proxy behavior.

### CI/CD

- [ ] Initialize and protect the source repository.
- [ ] Add CI checks for:
  - Backend restore, build, and tests
  - Frontend clean install, lint, tests, type-check, and production build
  - Dependency vulnerability scanning
  - Secret scanning
  - Migration validation
- [ ] Require successful checks before merging.
- [ ] Add staging deployment and smoke tests.
- [ ] Add an approved production deployment workflow with rollback.

### Database and storage

- [ ] Create an explicit production migration process.
- [ ] Prevent application instances from racing to apply migrations.
- [ ] Enable SQL connection retry for transient failures.
- [ ] Use managed SQL backups with point-in-time recovery.
- [ ] Define RPO and RTO targets.
- [ ] Move Data Protection keys to durable shared storage protected by KMS/Key Vault.
- [ ] Move uploaded files to durable object storage.
- [ ] Document key, database, and file restoration procedures.
- [ ] Perform and record a restoration drill.

### Observability

- [ ] Add structured JSON logging and correlation IDs.
- [ ] Add metrics and distributed tracing.
- [ ] Add readiness checks for SQL and required storage.
- [ ] Keep a separate lightweight liveness endpoint.
- [ ] Add dashboards and alerts for:
  - Elevated 4xx/5xx rates
  - Login failures and lockouts
  - Database errors and latency
  - Payroll calculation/finalization failures
  - Background-job failures
- [ ] Define log retention and PII-redaction rules.

Exit criteria:

- [ ] A clean environment can be deployed from CI without manual server edits.
- [ ] Database, encryption keys, and uploads can be restored.
- [ ] Operators can detect and investigate failures without accessing a developer machine.

---

## Phase 3 — Payroll domain and calculation engine

Priority: P0  
Goal: Implement the core payroll rules as a deterministic, testable domain.

### Data model

- [ ] Add monthly attendance/input entities and migrations.
- [ ] Add overtime, bonus, reimbursement, and one-time deduction entities.
- [ ] Add payroll-run and employee payroll-result entities.
- [ ] Add earning and deduction snapshot entities.
- [ ] Add payslip and disbursement records.
- [ ] Add tenant filters, indexes, uniqueness constraints, and concurrency controls to every new entity.
- [ ] Define immutable states for Draft, Calculated, Finalized, Reversed, and Paid.

### Calculation rules

- [ ] Resolve the effective salary structure for each employee and payroll period.
- [ ] Calculate payable days using the agreed attendance identity.
- [ ] Implement joining, exit, unpaid-leave, and mid-period proration.
- [ ] Calculate fixed and percentage-based salary components.
- [ ] Apply overtime, bonus, reimbursement, and one-time deductions.
- [ ] Apply deterministic monetary rounding.
- [ ] Prevent negative or otherwise invalid net salary.
- [ ] Produce warnings separately from blocking validation errors.
- [ ] Make recalculation idempotent.

### Automated tests

- [ ] Test full-month payroll.
- [ ] Test joining and exit during a payroll period.
- [ ] Test unpaid leave and working-day variants.
- [ ] Test mid-period salary revisions.
- [ ] Test overtime and one-time adjustments.
- [ ] Test percentage components and rounding boundaries.
- [ ] Test zero/negative-net validation.
- [ ] Test employee and component tenant isolation.
- [ ] Add randomized or property-based tests for payroll invariants.

Exit criteria:

- [ ] The same input always produces the same payroll result.
- [ ] Core calculation rules have documented examples and comprehensive automated tests.
- [ ] No finalized result depends on mutable employee or salary master data.

---

## Phase 4 — Monthly inputs and draft payroll workflow

Priority: P0  
Goal: Allow a Company Admin to prepare, calculate, and correct a monthly payroll.

- [ ] Add payroll-period creation and selection.
- [ ] Add a monthly attendance/input grid.
- [ ] Support bulk editing with clear validation feedback.
- [ ] Add overtime, bonus, reimbursement, and deduction inputs.
- [ ] Show missing salary structures and incomplete employee data before calculation.
- [ ] Add calculate and recalculate operations.
- [ ] Display gross pay, deductions, net pay, warnings, and blocking errors by employee.
- [ ] Add totals and reconciliation checks for the run.
- [ ] Prevent duplicate payroll runs for the same company and period.
- [ ] Add optimistic concurrency for simultaneous payroll editors.
- [ ] Audit material input and calculation changes.
- [ ] Add API integration tests using SQL Server.
- [ ] Add an end-to-end test for setup → employees → monthly inputs → calculation.

Exit criteria:

- [ ] A Company Admin can prepare and validate a complete monthly payroll.
- [ ] Invalid or incomplete payroll cannot proceed to finalization.

---

## Phase 5 — Finalization, payslips, history, and disbursement

Priority: P0  
Goal: Complete the payroll lifecycle while preserving an immutable audit trail.

- [ ] Add finalization with an explicit confirmation step.
- [ ] Snapshot all employee, salary, attendance, earning, deduction, and company-display data.
- [ ] Prevent edits to finalized payroll snapshots.
- [ ] Add controlled reversal with mandatory reason and authorization.
- [ ] Link reversals to the original run and preserve both audit records.
- [ ] Generate payslip PDFs from snapshots, not live employee data.
- [ ] Include required company, employee, pay-period, earning, deduction, and net-pay details.
- [ ] Add secure individual and bulk payslip download.
- [ ] Add payroll history with pagination and filters.
- [ ] Add disbursement states and payment reference recording.
- [ ] Add mark-paid and correction workflows.
- [ ] Add reconciliation totals and export formats required for the MVP.
- [ ] Add end-to-end tests for calculate → finalize → payslip → paid → history.

Exit criteria:

- [ ] A complete payroll can be finalized, distributed, marked paid, and audited.
- [ ] Historical payslips remain unchanged after employee or salary edits.
- [ ] Reversal is controlled, traceable, and does not destroy history.

---

## Phase 6 — Billing and subscription lifecycle

Priority: P1  
Goal: Turn the payroll product into an operable paid SaaS.

- [ ] Implement billable-employee counting.
- [ ] Implement subscription pricing, tax, proration, and rounding rules.
- [ ] Generate monthly subscription charges or invoices.
- [ ] Integrate the selected payment provider.
- [ ] Validate signed payment webhooks and make processing idempotent.
- [ ] Record payments without storing prohibited payment credentials.
- [ ] Implement Past Due, grace period, Suspended, Reactivated, and Cancelled transitions.
- [ ] Enforce read/write behavior consistently for each subscription state.
- [ ] Add customer-facing billing status, invoice, and payment history.
- [ ] Add Superadmin payment correction controls with mandatory audit reasons.
- [ ] Add scheduled background jobs for billing and subscription transitions.
- [ ] Add retries, dead-letter handling, and alerts for failed jobs/webhooks.
- [ ] Add billing and subscription integration tests.

Exit criteria:

- [ ] Subscription states transition automatically and idempotently.
- [ ] Payment records reconcile with the provider.
- [ ] Suspended and cancelled companies receive exactly the approved access level.

---

## Phase 7 — SaaS administration and customer lifecycle

Priority: P1  
Goal: Provide the controls needed to support multiple paying companies safely.

- [ ] Add Superadmin dashboard metrics.
- [ ] Add company search, filters, and pagination.
- [ ] Add suspend, reactivate, and cancel controls.
- [ ] Add plan and employee-limit management.
- [ ] Add secure Company Admin invitation and reset workflows.
- [ ] Add an immutable, searchable audit-log viewer.
- [ ] Add approved break-glass support access with reason, expiry, and prominent auditing.
- [ ] Add Company Admin profile and settings.
- [ ] Add customer self-service data export.
- [ ] Add company/account closure workflow.
- [ ] Add notification preferences and transactional email templates.
- [ ] Add analytics events for activation, setup completion, first payroll, and retention milestones.

Exit criteria:

- [ ] Normal customer-support operations do not require direct database changes.
- [ ] Every privileged Superadmin action is attributable and reviewable.

---

## Phase 8 — Privacy, compliance, and controlled launch

Priority: P1  
Goal: Validate legal, operational, and security readiness before real customers.

### Privacy and compliance

- [ ] Complete a DPDP Act data inventory and purpose assessment.
- [ ] Publish privacy policy, terms, and data-processing terms.
- [ ] Implement consent/notice where required.
- [ ] Define retention periods for payroll, audit, authentication, and uploaded data.
- [ ] Implement deletion/anonymization rules that preserve legally required payroll records.
- [ ] Document data-subject request handling.
- [ ] Define incident-response and breach-notification procedures.
- [ ] Review Indian payroll and payslip requirements with a qualified payroll/legal professional.

### Quality and resilience

- [ ] Add browser end-to-end tests for all critical roles and flows.
- [ ] Add accessibility testing and manual keyboard/screen-reader checks.
- [ ] Add load tests for login, employee lists, payroll calculation, and payslip generation.
- [ ] Test concurrent finalization and duplicate webhook delivery.
- [ ] Perform tenant-isolation and authorization penetration testing.
- [ ] Perform dependency and application security testing.
- [ ] Resolve all P0 and accepted P1 findings.

### Launch readiness

- [ ] Create production support and escalation procedures.
- [ ] Create customer onboarding and payroll-run documentation.
- [ ] Define service-level objectives and maintenance windows.
- [ ] Add feature flags and a rollback/kill-switch strategy.
- [ ] Run a full staging payroll with representative anonymized data.
- [ ] Run a pilot with a small number of approved companies.
- [ ] Review pilot incidents and feedback before broader launch.

Exit criteria:

- [ ] Security, privacy, backup, restore, and incident-response checks are signed off.
- [ ] The complete PRD acceptance flow passes in staging.
- [ ] A controlled pilot completes at least one full payroll cycle.

---

## Phase 9 — Post-launch improvements

Priority: P2  
Goal: Improve scale, usability, and commercial capability after the core service is stable.

- [ ] Add employee self-service and secure payslip delivery.
- [ ] Add payroll bank-file exports and provider integrations.
- [ ] Add configurable salary-component templates.
- [ ] Add richer reports and accounting exports.
- [ ] Add automated statutory calculations only after requirements are formally validated.
- [ ] Add mobile-optimized workflows.
- [ ] Add localization where customer demand justifies it.
- [ ] Add usage-based capacity planning and cost dashboards.
- [ ] Review tenant partitioning and caching as scale requires.

---

## Release gates

### Internal development

- [x] Local onboarding and employee-management foundation works.
- [ ] Phase 0 decisions complete.
- [ ] CI checks required on every change.

### Staging

- [ ] Phases 1–5 complete.
- [ ] Production-like SQL, key storage, object storage, and email configured.
- [ ] Full payroll lifecycle passes integration and end-to-end tests.

### Controlled paid pilot

- [ ] Phases 1–8 P0 items complete.
- [ ] No unresolved critical/high security findings.
- [ ] Backup restoration and incident-response exercises completed.
- [ ] Legal/payroll review completed.

### General availability

- [ ] Pilot results reviewed and launch risks accepted.
- [ ] Billing, support, monitoring, and customer lifecycle are fully operational.
- [ ] Capacity, security, and recovery objectives are demonstrably met.

---

## Progress log

Add dated entries when a phase or significant milestone changes.

- 2026-08-29: Initial phased SaaS roadmap created from the codebase and PRD audit.
