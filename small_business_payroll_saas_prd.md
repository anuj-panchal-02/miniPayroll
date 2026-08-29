# PRD: miniPayroll

**Product Name:** miniPayroll  
**Document Version:** 1.2  
**Product Stage:** MVP  
**Target Market:** Small businesses with 1–9 salaried employees  
**Primary Market:** India  
**Currency:** INR only (MVP)  
**Primary Business Model:** SaaS subscription charged per billable employee  
**Primary SaaS Owner:** Superadmin-controlled onboarding and plan management

### Changelog (v1.1 → v1.2)

- Named the product **miniPayroll**.
- Standardized database table naming: every table uses the prefix `mp_` followed by the table name (e.g. `mp_TblUser`, `mp_TblPayrollRun`). No unprefixed tables are permitted.

### Changelog (v1.0 → v1.1)

- Locked payroll calculation rules: daily-rate formula, attendance identity, join/exit proration, mid-month salary change rule.
- Added statutory deduction component types (PF, ESI, Professional Tax, TDS, LWF) as manual-amount lines printed on payslips. Automated statutory calculation and filing remain out of scope.
- Resolved recurring vs one-time contradiction: recurring items live on the salary structure; one-time items live on the payroll run. Removed recurring flags from bonuses and deductions.
- Resolved proration contradiction: defined exactly what "simple proration" means and put it in scope with a functional requirement.
- Specified billing operations: manual offline collection, Superadmin-recorded payments, GST invoice reference field, 7-day default grace period, non-gameable billable-count rule.
- Added payroll reversal (correction path) and salary disbursement tracking (Paid/Unpaid per employee).
- Removed `Reviewed` as a persisted payroll state. States are now Draft → Calculated → Finalized (→ Reversed).
- Fixed contradictions: single Basic plan at launch (Pro removed from all MVP examples), INR-only currency, hardcoded Paid/Unpaid leave (removed `TblLeaveType` from MVP model), incentives folded into bonus types, company details entered once by the Company Admin.
- Set a hard MVP employee cap of 20 per company (default limit 9).
- Removed restaurants/cafes from primary MVP customers (daily-wage model not supported in MVP).
- Consolidated the five monthly-input screens into a single Monthly Inputs screen. MVP screen count reduced.
- Replaced EF DB-First/EDMX with EF Core. Added DPDP Act, encryption-at-rest for bank details, Superadmin MFA, password policy, lockout, and break-glass support access requirements.
- Tightened functional requirements: added FRs for proration, password reset, payslip PDF, suspended-tenant behavior, payment recording, reversal, disbursement, statutory components, and analytics events.

---

## 1. Product Summary

**miniPayroll** is a multi-tenant payroll SaaS platform for small businesses that need a simple way to manage employees and process monthly payroll.

The SaaS owner (Superadmin) creates company accounts, creates company-admin credentials, assigns plans, manages employee limits and controls subscription status. Each company admin then manages their employees, salary structures, attendance, leave, overtime, bonuses, deductions and monthly payroll.

The product is intentionally payroll-first rather than a full HRMS.

### Core Promise

> **Create a company, add employees, enter monthly changes, calculate payroll, review it, finalize it and generate payslips without maintaining spreadsheets.**

---

# 2. Problem Statement

Businesses with fewer than 10 employees often calculate salaries manually using spreadsheets, calculators, attendance records and informal notes. Every payroll cycle may require calculations for paid leave, unpaid absence, overtime, bonuses, advances and deductions.

Existing payroll and HRMS products are often designed for larger organizations and can introduce unnecessary complexity, configuration and cost for very small businesses.

### Core Problem

> Small businesses need a fast, simple and affordable way to calculate, review and finalize monthly payroll accurately, while the SaaS owner needs a straightforward way to onboard companies and monetize the service per employee.

---

# 3. Product Vision

Build miniPayroll as the simplest payroll SaaS for businesses with fewer than 10 employees, while providing a centrally managed SaaS platform that supports multiple companies, employee-based billing and controlled onboarding.

The product should make payroll feel like a short monthly task rather than an accounting project.

---

# 4. Product Goals

## 4.1 Business Goals

- Launch as a multi-tenant SaaS product.
- Charge customers based on billable employees.
- Let the SaaS owner control company creation and subscriptions.
- Reduce manual payroll work for small businesses.
- Create a recurring monthly revenue model.
- Establish a foundation for later self-service billing and broader HR features.

## 4.2 User Goals

Company admins should be able to:

- Manage employees.
- Configure salary structures.
- Record monthly attendance and leave.
- Record overtime.
- Add bonuses and deductions.
- Calculate payroll automatically.
- Review payroll before finalization.
- Finalize payroll.
- Generate payslips.
- Record which salaries have been paid out.
- View payroll history.

---

# 5. Target Customers

## Primary Customer

Businesses with 1–9 **monthly-salaried** employees such as:

- Retail shops
- Small agencies
- Local service businesses
- Small offices
- Clinics and small professional practices
- Family-owned businesses
- Small contractors with salaried staff

## Explicitly Not Targeted in MVP

- Businesses paying **daily or hourly wages** (e.g. many restaurants, cafes and construction crews). The MVP payroll engine supports monthly salary only. Daily-wage support is a candidate for Version 2.

## Buyer

Usually the owner, manager or accountant of the business.

## Daily/Monthly User

Usually the company owner, office administrator or accountant.

---

# 6. User Roles

## 6.1 Superadmin

The SaaS owner/platform operator.

### Responsibilities

- Create companies.
- Create company-admin accounts.
- Assign plans.
- Set employee limits (within the platform cap).
- Change plans.
- View subscription status.
- Record subscription payments.
- Activate or suspend companies.
- View company details.
- View employee counts.
- View payroll activity.
- Reset company-admin credentials.
- Reverse a finalized payroll run on a company's request (audited).
- Access company data for support via break-glass access, with audit logging.
- Manage SaaS plans.
- View SaaS-level metrics.

### Restrictions

- Should not accidentally operate payroll as an ordinary company admin.
- Support access to company data is break-glass: it must be explicitly initiated, reason-tagged and audit-logged. It is not a routine browsing mode.
- Must use MFA (see Security Requirements).

---

## 6.2 Company Admin

The customer/business administrator. **MVP supports exactly one Company Admin account per company.** Multiple admin users per company is a future capability.

### Responsibilities

- Complete company setup.
- Manage employees.
- Configure salary structures.
- Enter monthly inputs: attendance, leave, overtime, bonuses, deductions.
- Run payroll.
- Review payroll.
- Finalize payroll.
- Generate and download payslips.
- Record salary disbursement status.
- View payroll history.

### Restrictions

- Cannot create another company.
- Cannot access another company's data.
- Cannot change SaaS pricing.
- Cannot increase their subscription limits (must contact the SaaS owner).
- Cannot manage platform-level settings.
- Cannot edit or delete a finalized payroll run (may request reversal via the SaaS owner).

---

## 6.3 Employee

Employee self-service is **out of scope for MVP**.

Potential future capabilities:

- Login
- View payslips
- View profile
- View leave balances
- Submit leave requests

---

# 7. SaaS Account and Tenant Model

Each customer company is a separate tenant.

```text
Superadmin
   |
   +-- Company A
   |      +-- Company Admin
   |      +-- Employees
   |      +-- Payroll
   |
   +-- Company B
   |      +-- Company Admin
   |      +-- Employees
   |      +-- Payroll
   |
   +-- Company C
          +-- Company Admin
          +-- Employees
          +-- Payroll
```

### Tenant Isolation Requirement

Every company-owned business record must be associated with `CompanyId` or an equivalent tenant identifier.

The backend must enforce tenant isolation on every query and mutation. Company A users must never be able to retrieve or modify Company B data.

Frontend filtering alone is not a security mechanism.

---

# 8. Core Business Model

## 8.1 Pricing Model

The primary billing metric is the number of billable employees.

Example:

```text
Price per employee: ₹49/month
Billable employees: 6
Monthly subscription: ₹294
```

The exact price is configurable by Superadmin.

### Billable-Count Rule (MVP)

> **Billable employees for a billing period = the number of distinct employees included in any payroll run finalized during that period.** If no payroll run was finalized in the period, the fallback is the **maximum active-employee count during the period**.

This rule is deliberately not "active count at end of period", because that can be gamed by deactivating employees after payroll is run. The rule is fixed for MVP (not configurable).

## 8.2 Plan Model

**MVP launches with exactly one public plan: Basic.** The Plan entity is retained in the data model so future plans and pricing changes do not require a redesign, but no second plan is exposed anywhere in the MVP UI, examples or dashboards.

### Basic (the only MVP plan)

- Payroll
- Attendance
- Leave
- Overtime
- Bonuses
- Deductions (including statutory deduction lines)
- Payslips
- Payroll history
- Disbursement tracking

Future plans (e.g. a Pro tier with employee self-service, email payslips, advanced reports, bank payment export) belong to the roadmap (Section 38), not the MVP.

---

# 9. Subscription Lifecycle

A company subscription supports these statuses:

```text
Pending
  ↓
Active
  ↓
Past Due
  ↓
Suspended
  ↓
Cancelled
```

### MVP Behavior

- **Pending:** company created but not activated. No login permitted for the Company Admin.
- **Active:** payroll and normal usage permitted.
- **Past Due:** payment is overdue but the grace period is still running. Full functionality remains available. A payment-due banner is shown to the Company Admin.
- **Suspended:** the Company Admin can log in, view all historical data and download existing payslips, but cannot create or finalize a payroll run, and cannot add or edit employees.
- **Cancelled:** subscription terminated. Login disabled. Data retained per the retention policy (Section 31).

### Grace Period

- Default grace period: **7 days** after the payment due date, after which the Superadmin may suspend the company.
- The grace period length is configurable per company by the Superadmin.
- Transitions between statuses are performed manually by the Superadmin in MVP (no automated dunning).

---

# 10. Employee Limit and Billing

Because the service is charged per employee, employee limits must be enforced.

### Limits

- **Default employee limit per company: 9** (matches the target segment).
- **Hard platform cap in MVP: 20 employees per company.** The Superadmin can raise a company's limit up to 20 but not beyond. This keeps the product honest about the segment its UX is designed for; larger tenants are a roadmap decision, not a support ticket.

Attempting to create an employee beyond the company's limit displays:

> Employee limit reached. Your current plan supports N employees. Please contact your service provider to increase the limit.

The Superadmin can then increase the limit (up to the platform cap).

### Billing Operations (MVP)

Self-service payment is out of scope. Billing works as follows:

1. At the end of each billing period (calendar month), the system computes the subscription amount = price per employee × billable employees (rule in Section 8.1).
2. The Superadmin collects payment **offline** (UPI, NEFT, cash — outside the product).
3. The Superadmin records the payment against the billing period: amount, date, payment mode, and an optional **invoice/GST reference number**. GST invoice generation itself is outside the product in MVP (issued manually by the SaaS owner); the product stores the reference.
4. Companies created mid-month are billed from their **activation date**, prorated by calendar days for the first period.

---

# 11. End-to-End SaaS Workflow

## 11.1 Superadmin Workflow

```text
Superadmin Login
      ↓
Superadmin Dashboard
      ↓
Create Company (name + contact only)
      ↓
Assign Plan
      ↓
Set Employee Limit
      ↓
Create Company Admin (email + temporary password)
      ↓
Activate Company
      ↓
System emails credentials to Company Admin
```

Credential delivery: the system emails the Company Admin a temporary password. The Company Admin **must change the password at first login**.

## 11.2 Company First-Login Workflow

```text
Company Admin Login (temporary password)
      ↓
Forced Password Change
      ↓
Welcome / Setup Wizard
      ↓
Company Details (address, logo, contact)
      ↓
Payroll Settings (working days, weekly off, daily-rate method)
      ↓
Add Employees
      ↓
Configure Salaries
      ↓
Setup Complete
      ↓
Company Dashboard
```

The Superadmin enters only the company name and contact when creating the company. All remaining company details are entered **once**, by the Company Admin, in the setup wizard. There is no duplicate data entry.

The first-login setup should take as little time as possible (target: under 15 minutes).

## 11.3 Monthly Payroll Workflow

```text
Select Payroll Month
      ↓
Load Active Employees
      ↓
Enter Monthly Inputs (attendance, leave, overtime, bonuses, deductions — one screen)
      ↓
Calculate Payroll
      ↓
Review Payroll (warnings and errors)
      ↓
Finalize Payroll
      ↓
Generate Payslips
      ↓
Record Disbursement (mark salaries paid)
      ↓
Payroll History
```

---

# 12. Company Setup

The company admin configures, once, during the setup wizard:

- Company name (pre-filled from Superadmin entry, editable)
- Address
- Phone
- Email
- Logo (image upload, used on payslips)
- Payroll cycle (monthly; the only MVP option)
- Working days per month convention
- Weekly holidays
- Daily-rate calculation method (see Section 19; one choice from two options)

Currency is **INR, fixed**, not configurable in MVP. Multi-currency arrives with multi-country payroll, which is out of scope.

Most settings are configured once and reused for future payroll periods.

---

# 13. Employee Management

The company admin can create employees with:

## Personal Information

- Employee ID
- Full name
- Date of birth, optional for MVP
- Phone
- Email
- Address

## Employment Information

- Designation
- Department, optional
- Employment type (MVP: **Full-time monthly salaried** only; the field exists for future types)
- Joining date
- Exit date (optional; set when an employee leaves)
- Employment status (Active / Inactive)

## Payment Information

- Bank name
- Bank account number (encrypted at rest)
- IFSC (encrypted at rest)
- UPI ID, optional

## Payroll Information

- Monthly salary
- Salary structure (components; see Section 14)
- Overtime rate (company default or employee-specific)

### Employee Status Rules

- **Active** employees are included in new payroll runs.
- An employee leaving during a month keeps status **Active with an exit date** until the payroll run covering their final month is finalized; the run prorates their salary to the exit date. After that run is finalized, the admin sets them **Inactive**.
- **Inactive** employees are excluded from new payroll runs.

---

# 14. Salary Structure

The system must support configurable salary components. Every component is either **recurring** (part of the salary structure, applied every month automatically) or **one-time** (entered on a specific payroll run). This is the single, global rule; there are no per-record recurring flags anywhere else in the product.

## Recurring Earnings (on the salary structure)

Examples:

- Basic Salary
- HRA
- Conveyance Allowance
- Special Allowance
- Other Allowance

## Recurring Deductions (on the salary structure)

Examples:

- Provident Fund (PF) — manual amount
- ESI — manual amount
- Professional Tax — manual amount
- Labour Welfare Fund (LWF) — manual amount
- Other recurring deduction

## One-Time Earnings (on the payroll run)

- Overtime (calculated)
- Bonus (all bonus types, including incentives — see Section 17)

## One-Time Deductions (on the payroll run)

- Unpaid leave deduction (calculated)
- Salary advance recovery
- Loan installment
- TDS — manual amount
- Other deduction

## Component Value Types

- Fixed amount
- Percentage of Basic Salary

### Statutory Components in MVP

PF, ESI, Professional Tax, TDS and LWF are supported as **named deduction components with manually entered amounts**. The MVP does **not** calculate statutory amounts, apply thresholds, or file returns — the admin (or their accountant) enters the amounts, and the system carries them through calculation, review, snapshots and payslips. Automated statutory calculation is Version 3 (Section 38).

This matters because Indian small businesses that already deduct PF or PT in spreadsheets will not adopt a product whose payslips cannot show those lines.

---

# 15. Attendance and Leave

The MVP uses a simple monthly-summary attendance model suitable for very small businesses. There is no daily attendance register, shift management, or device integration.

Example:

| Employee | Working Days | Present | Paid Leave | Unpaid Leave |
|---|---:|---:|---:|---:|
| Rahul | 26 | 24 | 1 | 1 |
| Priya | 26 | 26 | 0 | 0 |

### Attendance Identity (validation rule)

For every employee, the entry must satisfy:

```text
Present + Paid Leave + Unpaid Leave = Working Days
```

Entries that do not satisfy this identity are a **validation error** and block finalization. Half days are recorded as 0.5. Weekly offs and holidays are excluded from Working Days by definition and are never entered per employee.

### Leave Types for MVP

Exactly two, hardcoded:

- Paid Leave (does not reduce salary)
- Unpaid Leave (reduces salary per Section 19)

Configurable leave categories, leave balances and accruals are future scope. The MVP data model does not include a leave-type table.

---

# 16. Overtime

The system allows employees to have company-default or employee-specific overtime rates.

Example:

```text
Employee: Rahul
Overtime Hours: 10
Rate: ₹150/hour
Overtime Amount: ₹1,500
```

The system calculates overtime as `hours × rate` and adds it to gross earnings. Overtime is not part of Basic Salary and does not affect the daily rate used for unpaid-leave deductions.

---

# 17. Bonuses

The system supports **one-time** bonuses entered on a payroll run. Incentives are a bonus type, not a separate module or formula line.

Bonus types:

- Festival Bonus
- Performance Bonus
- Attendance Bonus
- Incentive
- Other Bonus

Fields:

- Employee
- Bonus type
- Amount
- Payroll month
- Notes

Genuinely recurring payments belong on the salary structure as a recurring earning component (Section 14), not as repeated bonuses.

---

# 18. Deductions

The system supports **one-time** deductions entered on a payroll run:

- Unpaid leave deduction (calculated automatically; not manually entered)
- Salary advance recovery
- Loan installment
- TDS
- Other deduction

Each manually entered deduction includes:

- Employee
- Deduction type
- Amount
- Payroll month
- Notes

### Advances and Loans — MVP Boundary

Advances and loans are recorded as **one-time deduction amounts for the selected month only**. The MVP does **not** track outstanding balances, schedules or remaining installments — that is a loan-module feature for a future version. The UI must label these fields so admins understand no balance is being tracked (preventing accidental double-deduction assumptions).

Recurring statutory deductions (PF, ESI, PT, LWF) belong on the salary structure (Section 14).

---

# 19. Payroll Calculation Engine

The payroll engine is the most important domain component of the MVP. All rules below are exact and testable.

## 19.1 Daily Rate

The company chooses **one** of exactly two methods during setup (Principle 6: defaults over configuration):

```text
Method A (default): Daily Rate = Monthly Salary ÷ Calendar Days in the payroll month
Method B:           Daily Rate = Monthly Salary ÷ 30
```

"Monthly Salary" here means the sum of all recurring earning components. No other daily-rate methods exist in MVP.

## 19.2 Calculation

```text
Recurring Earnings = Σ recurring earning components (prorated if applicable, see 19.3)

Gross Earnings
= Recurring Earnings
+ Overtime
+ Bonuses
```

```text
Unpaid Leave Deduction = Daily Rate × Unpaid Leave Days
```

```text
Total Deductions
= Unpaid Leave Deduction
+ Recurring Deductions (PF, ESI, PT, LWF, other)
+ One-Time Deductions (advance, loan, TDS, other)
```

```text
Net Salary = Gross Earnings − Total Deductions
```

Rounding: every component line is rounded to the nearest rupee (half up); Net Salary is the sum of rounded lines.

## 19.3 Proration (joining or leaving mid-month)

MVP proration is **simple and calendar-day based**, regardless of the daily-rate method chosen above:

```text
Prorated Recurring Earnings
= Recurring Earnings × (Days Employed in Month ÷ Calendar Days in Month)
```

- **Joining mid-month:** Days Employed = calendar days from joining date to month end, inclusive.
- **Leaving mid-month:** Days Employed = calendar days from month start to exit date, inclusive.
- Recurring deductions are prorated by the same ratio. One-time items are never prorated.

Anything beyond this (split periods, per-component proration rules, LOP calendars) is **complex proration** and explicitly out of scope for MVP.

## 19.4 Mid-Month Salary Change

If an employee's salary structure changes during a payroll month, the **entire month uses the structure effective on the last calendar day of the payroll period**. Split-month calculations are out of scope for MVP. The review screen shows a "salary changed since last month" warning so the admin can verify intent.

---

# 20. Payroll Run

A Payroll Run represents one company's payroll for one payroll period.

Example:

```text
Payroll Period: August 2026
Company: ABC Traders
Employees: 6
Status: Draft
```

### Payroll States

```text
Draft
  ↓
Calculated
  ↓
Finalized
  ↓ (correction only, Superadmin action)
Reversed
```

- **Draft:** run created, monthly inputs being entered.
- **Calculated:** engine has produced results; admin is reviewing. Editing any input returns the run to Draft.
- **Finalized:** immutable (Section 22). Review is an activity the admin performs on a Calculated run, not a persisted state — a single admin reviewing their own work does not need a separate approval step.
- **Reversed:** the correction path (Section 22.1).

Only one non-reversed payroll run may exist per company per period.

---

# 21. Payroll Review

Before finalization, the company admin sees a summary such as:

| Employee | Gross | Deductions | Net Salary |
|---|---:|---:|---:|
| Rahul | ₹34,500 | ₹3,154 | ₹31,346 |
| Priya | ₹28,000 | ₹0 | ₹28,000 |
| Amit | ₹26,500 | ₹1,000 | ₹25,500 |

Each employee has a detailed component-level breakdown.

### Warnings (do not block finalization)

- Salary changed compared to the previous month.
- Net salary differs from the previous month by more than 20%.
- Employee has more than 5 unpaid leave days.
- Employee was prorated (joined or left during the month).
- Net salary is zero.

### Errors (block finalization)

- Attendance not entered for one or more active employees.
- Attendance identity violated (Present + Paid Leave + Unpaid Leave ≠ Working Days).
- An employee has no salary structure configured.
- Net salary is negative.
- Company subscription is Suspended or Cancelled.

---

# 22. Finalization

When the company admin selects **Finalize Payroll**:

1. Validate payroll (all errors in Section 21 must be clear).
2. Save calculated values.
3. Store salary component snapshots (every earning and deduction line).
4. Store key calculation inputs (attendance figures, daily-rate method, rates used).
5. Lock payroll records.
6. Record finalization user and timestamp.
7. Mark payroll as `Finalized`.

Historical payroll must not change when future salary structures change.

For example:

```text
July salary: ₹25,000
August salary: ₹30,000
```

July payroll must continue to show ₹25,000 even after the employee's current salary is changed to ₹30,000.

## 22.1 Correction Path: Reversal

Immutability needs a legitimate correction path, otherwise support will end up editing the database and breaking the snapshot guarantee.

- Only the **Superadmin** can reverse a finalized run, on the company's request.
- Reversal marks the run `Reversed` (it is never deleted or edited), records the reversing user, timestamp and a mandatory reason, and writes an audit log entry.
- Payslips belonging to a reversed run are watermarked "Reversed" and remain downloadable.
- After reversal, the company admin can create a new payroll run for the same period.

---

# 23. Payslips

After payroll is finalized, the system generates payslips as PDFs.

### Payslip Rendering Rule

The payslip renders the **snapshot component lines stored on the payroll run** — every earning line and every deduction line by name and amount, exactly as finalized. There is no hardcoded field list; if a company's structure has PF and PT lines, the payslip shows PF and PT lines.

### Payslip Contents

- Company logo and information
- Employee information (name, Employee ID, designation)
- Payroll month
- All earning lines (from snapshot)
- Gross earnings
- All deduction lines (from snapshot), including statutory lines
- Total deductions
- Net salary (in figures and words)
- Payment status, if recorded (Paid via Bank/UPI/Cash on date)

Payslips are downloadable as PDF, individually and as a single combined PDF for the whole run.

---

# 24. Payroll History and Disbursement

## 24.1 History

The company admin can view previous payroll runs.

| Month | Employees | Gross | Deductions | Net | Status |
|---|---:|---:|---:|---:|---|
| Aug 2026 | 7 | ₹1,85,000 | ₹12,500 | ₹1,72,500 | Finalized |
| Jul 2026 | 7 | ₹1,80,000 | ₹8,200 | ₹1,71,800 | Finalized |

Historical payroll is read-only after finalization.

## 24.2 Disbursement Tracking

Finalizing a payroll is not the same as paying salaries. After finalization, the admin can mark each employee's salary as:

- **Paid** — with payment mode (Bank transfer / UPI / Cash) and payment date
- **Unpaid** (default)

Disbursement status is metadata attached to the finalized run; it is editable after finalization and does **not** modify any payroll amount. Bank payment file export is Version 2.

---

# 25. Superadmin Dashboard

The Superadmin dashboard focuses on SaaS operations, not individual payroll tasks.

### Metrics

- Total companies
- Active companies
- Suspended companies
- Total billable employees
- Active subscriptions
- Past-due subscriptions
- Monthly recurring revenue
- New companies this month
- Payroll runs this month

### Company Table

| Company | Employees | Plan | Status | Billing | Admin |
|---|---:|---|---|---|---|
| ABC Traders | 7 | Basic | Active | Paid | Raj |
| XYZ Retail | 4 | Basic | Active | Paid | Priya |
| PQR Services | 9 | Basic | Active | Pending | Amit |

### Superadmin Actions

- View company
- Edit company
- Change employee limit (up to platform cap)
- Record payment
- Activate
- Suspend
- Reset admin credentials
- Reverse a finalized payroll run (with reason)
- View subscription
- View payroll activity
- Initiate break-glass support access (reason required, audited)

---

# 26. Company Dashboard

The company dashboard should answer one question:

> **What do I need to do to finish this month's payroll?**

### Example

```text
Employees: 7

August 2026 Payroll
Status: Draft

Gross Payroll: ₹1,85,000
Deductions: ₹12,500
Net Payroll: ₹1,72,500

[Process Payroll]
```

### Navigation (MVP)

```text
Dashboard

Employees
  ├── Employee List
  └── Salary Structures

Payroll
  ├── Monthly Inputs   (attendance, leave, overtime, bonuses, deductions — one screen)
  ├── Current Payroll  (calculate, review, finalize)
  └── Payroll History  (runs, payslips, disbursement)

Settings
```

The five monthly-input areas are **one screen** with an employee-per-row grid, not five separate menu items. For a company with 5 employees, the entire month's data entry should happen on a single surface. This is what makes Principle 7 (fast monthly workflow) real rather than aspirational.

---

# 27. Functional Requirements

## Authentication and Authorization

**FR-001** The system must support Superadmin authentication with MFA.
**FR-002** The system must support Company Admin authentication.
**FR-003** The system must enforce role-based authorization.
**FR-004** Company data must be isolated by tenant on every backend operation.
**FR-005** Passwords must be securely hashed (bcrypt/argon2 class).
**FR-006** The system must force a password change on first Company Admin login.
**FR-007** The system must support self-service password reset via email for Company Admins.
**FR-008** The system must enforce a password policy, account lockout after repeated failures, and session/token expiration.

## Company Management

**FR-009** Superadmin must be able to create a company (name and contact only).
**FR-010** Superadmin must be able to create exactly one Company Admin account per company.
**FR-011** Superadmin must be able to assign the plan.
**FR-012** Superadmin must be able to set or change employee limits, up to the platform cap of 20.
**FR-013** Superadmin must be able to activate or suspend companies.
**FR-014** Company Admin must be able to complete company setup, including logo upload, in a first-login wizard.
**FR-015** A suspended company must retain read access to history and payslips but must be blocked from creating or finalizing payroll runs and from adding or editing employees.

## Subscription Management

**FR-016** The system must store each company's subscription and status.
**FR-017** The system must compute billable employees per the rule in Section 8.1.
**FR-018** The system must calculate the subscription amount from billable employees and per-employee price.
**FR-019** The system must prevent adding employees beyond the company limit.
**FR-020** Superadmin must be able to record payments (amount, date, mode, invoice/GST reference) against a billing period.
**FR-021** The system must support the subscription statuses and grace period defined in Section 9.

## Employee Management

**FR-022** Company Admin must be able to add employees.
**FR-023** Company Admin must be able to edit employee records.
**FR-024** Company Admin must be able to activate/deactivate employees and record joining and exit dates.
**FR-025** The system must track employee history needed for payroll.
**FR-026** Bank account numbers and IFSC codes must be encrypted at rest.

## Payroll Management

**FR-027** Company Admin must be able to configure salary structures with recurring earning and deduction components, including named statutory components (PF, ESI, PT, LWF).
**FR-028** Company Admin must be able to enter monthly attendance, leave, overtime, bonuses and deductions on a single monthly-inputs screen.
**FR-029** The system must validate the attendance identity (Section 15) for every employee.
**FR-030** The system must calculate payroll per the rules in Section 19, including daily rate, unpaid-leave deduction and rounding.
**FR-031** The system must prorate salary for employees joining or leaving mid-month per Section 19.3.
**FR-032** The system must apply the mid-month salary change rule of Section 19.4.
**FR-033** The system must present a payroll review with the warnings and blocking errors defined in Section 21.
**FR-034** The system must block finalization while any error condition exists.
**FR-035** The system must allow payroll finalization with full component snapshots and calculation inputs.
**FR-036** Finalized payroll must be immutable.
**FR-037** Superadmin must be able to reverse a finalized payroll run with a mandatory reason; reversal must be audited and must permit a new run for the same period.
**FR-038** The system must generate payslips as PDFs rendered from snapshot lines, individually and combined per run.
**FR-039** The system must store payroll history.
**FR-040** Company Admin must be able to record per-employee disbursement status (Paid/Unpaid, mode, date) on finalized runs without altering payroll amounts.

## Audit

**FR-041** The system must record critical administrative actions (company create/suspend/activate, limit changes, plan changes, credential resets, payments recorded).
**FR-042** The system must record payroll finalization and reversal user and timestamp.
**FR-043** Superadmin break-glass access to company data must require a reason and be fully audit-logged.

## Analytics

**FR-044** The system must emit product analytics events for: company activated, setup completed, first payroll finalized, payroll run started, payroll run finalized, monthly login. (The success metrics in Section 36 are not measurable without these.)

---

# 28. Business Rules

### Employee Billing

- Billable employees are counted per the rule in Section 8.1 (finalized-run head count; fallback max active count).
- Billable-employee count determines the monthly subscription amount.
- This rule is fixed in MVP (not configurable).

### Employee Status

- Active employees are included in new payroll runs.
- Inactive employees are excluded from new payroll runs.
- Leaving employees remain Active (with exit date) through their final prorated run, then are deactivated (Section 13).

### Joining During Month

Prorated per Section 19.3. (Covered by FR-031.)

### Leaving During Month

Prorated per Section 19.3. (Covered by FR-031.)

### Paid Leave

Paid leave does not reduce salary.

### Unpaid Leave

Unpaid leave creates a salary deduction of Daily Rate × Unpaid Leave Days (Section 19).

### Overtime

Overtime (hours × rate) is added to earnings; it does not affect the daily rate.

### Bonus

Bonuses (including incentives) are one-time amounts applied to the selected payroll period only.

### Deduction

One-time deductions reduce net salary in the selected payroll period only. Recurring deductions live on the salary structure.

### Finalized Payroll

Finalized payroll cannot be edited. The only correction path is Superadmin reversal plus a new run (Section 22.1).

---

# 29. Database Model

### Naming Convention

Every database table **must** be named `mp_` + table name. No unprefixed tables are permitted.

```text
mp_TblUser
mp_TblPayrollRun
```

This applies to all current tables and any table added later.

A high-level model is:

```text
SaaS Layer

mp_TblUser
mp_TblRole
mp_TblCompany
mp_TblPlan
mp_TblSubscription
mp_TblPayment
mp_TblAuditLog

Payroll Layer

mp_TblEmployee
mp_TblSalaryStructure
mp_TblSalaryComponent
mp_TblEmployeeSalaryComponent
mp_TblMonthlyAttendance
mp_TblOvertime
mp_TblBonus
mp_TblDeduction
mp_TblPayrollRun
mp_TblPayrollEmployee
mp_TblPayrollEarning
mp_TblPayrollDeduction
mp_TblPayrollDisbursement
mp_TblPayslip
```

Notes:

- Leave is captured inside `mp_TblMonthlyAttendance` (working days, present, paid leave, unpaid leave). There is no leave-type table in MVP because leave types are hardcoded.
- `mp_TblPayrollDisbursement` stores per-employee payment status against a finalized run.
- `mp_TblPayrollRun` carries status including `Reversed`, plus reversal reason/user/timestamp.

### Key Relationships

```text
mp_TblPlan
   ↓
mp_TblSubscription
   ↓
mp_TblCompany
   ├── mp_TblUser
   ├── mp_TblEmployee
   ├── mp_TblMonthlyAttendance
   ├── mp_TblOvertime
   ├── mp_TblBonus
   ├── mp_TblDeduction
   └── mp_TblPayrollRun
          ↓
     mp_TblPayrollEmployee
          ├── mp_TblPayrollEarning
          ├── mp_TblPayrollDeduction
          └── mp_TblPayrollDisbursement
```

All company-owned records should carry `CompanyId` directly or be reachable through a secure tenant relationship.

---

# 30. Historical Payroll Snapshot Requirement

This is a critical architectural requirement.

Finalized payroll must preserve the exact salary values and calculations used at the time the payroll was processed.

Do not calculate old payroll by reading the employee's current salary structure.

### Example

```text
Employee current salary:
₹30,000

Finalized July payroll:
₹25,000
```

If the employee's salary is changed later, July must remain ₹25,000.

Required approach:

- Store calculated payroll values in payroll-specific tables.
- Store every earning and deduction line as a snapshot.
- Store key calculation inputs (attendance, daily-rate method, overtime rate) required for auditability.
- Payslips render exclusively from snapshots.

---

# 31. Security and Compliance Requirements

The product handles salary and financial information, so tenant isolation and authorization are critical.

### Security Requirements

- Secure password storage (bcrypt/argon2 class).
- Password policy, account lockout after repeated failed attempts.
- **MFA mandatory for Superadmin accounts** (this role can see every company's pay data).
- HTTPS in production.
- Role-based access control.
- Tenant-aware authorization on every company data operation.
- Audit logs for sensitive actions.
- No company-to-company data access.
- **Bank account numbers and IFSC codes encrypted at rest.**
- Protected administrative APIs.
- Session/token expiration and revocation.
- Rate limiting for authentication endpoints.
- Superadmin support access is break-glass: explicitly initiated, reason-tagged, audited (FR-043).

### Compliance (India, DPDP Act)

- Employee personal data is processed on behalf of the customer company; document lawful basis and roles in the terms of service.
- **Data retention:** cancelled companies' data is retained for 12 months, then permanently deleted. Companies may request earlier deletion.
- Support a data deletion request path (manual process in MVP, but the deletion itself must be complete and verifiable).
- Salary data access by Superadmin is limited to break-glass support access.

### Backup and Recovery

- Automated daily database backups.
- Recovery point objective (RPO): 24 hours. Recovery time objective (RTO): 24 hours. Both acceptable for a monthly-cycle product at MVP stage.

---

# 32. Non-Functional Requirements

### Performance

For a company with fewer than 10 employees, payroll calculation must complete in under 2 seconds; typical page loads under 1 second under expected operating conditions.

### Availability

The application should be reliable enough for monthly payroll processing, with the backups and monitoring defined in Section 31. Highest sensitivity is month-end (payroll days).

### Usability

A new Company Admin should be able to complete initial setup without training, in under 15 minutes.

### Scalability

The architecture should support many small companies even though each individual customer has fewer than 10 employees.

### Maintainability

The system should separate:

- SaaS management
- Authentication/authorization
- Subscription/billing
- Payroll domain logic
- Reporting/document generation

---

# 33. Recommended Technology Stack

### Frontend

- React
- Next.js

### Backend

- .NET 8 Web API

### Database

- SQL Server

### ORM

- **Entity Framework Core** (code-first with migrations, or scaffolded from an existing database). EDMX/DB-First is an EF6-era approach and must not be used with .NET 8; it would obstruct migrations, global tenant query filters and snapshot table evolution.

### Authentication

- ASP.NET Core Identity or a secure JWT/session solution, with MFA support for Superadmin

### PDF Generation

- Server-side PDF generation for payslips (e.g. QuestPDF)

### Email

- Transactional email provider for credential delivery and password reset

### Deployment

```text
Next.js
   ↓
.NET 8 Web API
   ↓
SQL Server
```

The application should be designed as a multi-tenant SaaS from the beginning, with tenant scoping enforced in the data access layer (e.g. EF Core global query filters keyed by `CompanyId`) in addition to authorization checks.

---

# 34. MVP Screens

## Superadmin (8)

1. Superadmin Login
2. Dashboard
3. Company List
4. Create Company
5. Company Details (admin management, subscription, employee limit, payments, payroll activity)
6. Plan Settings (single plan: price per employee)
7. Record Payment
8. Audit Log

## Company Admin (11)

9. Login (incl. forced password change / reset flows)
10. First-Time Setup Wizard
11. Company Dashboard
12. Employee List
13. Add/Edit Employee
14. Salary Structure
15. Monthly Inputs (attendance, leave, overtime, bonuses, deductions — single grid)
16. Payroll Review (calculate, warnings/errors, finalize)
17. Payroll History (incl. disbursement marking)
18. Payslip View/Download
19. Company Settings

19 screens total (down from 27 in v1.0), consistent with Principle 2 and Principle 7.

---

# 35. MVP Acceptance Criteria

The MVP is considered functional when the following complete flow works end-to-end:

```text
Superadmin Login
      ↓
Create Company
      ↓
Assign Plan + Set Employee Limit
      ↓
Create Company Admin (credentials emailed)
      ↓
Company Admin First Login + Forced Password Change
      ↓
Complete Setup Wizard
      ↓
Add 5 Employees
      ↓
Configure Salaries (incl. a PF and a PT deduction component)
      ↓
Create August Payroll
      ↓
Enter Monthly Inputs (attendance/leave/overtime/bonus/deduction on one screen)
      ↓
Calculate Payroll
      ↓
Review (see at least one warning; clear at least one blocking error)
      ↓
Finalize Payroll
      ↓
Generate 5 Payslips (PDF, showing statutory lines)
      ↓
Mark Salaries Paid
      ↓
View Payroll History
```

The system must also demonstrate that:

- Tenant isolation works (a Company A token cannot read Company B data).
- Employee limits are enforced, and cannot be set above the platform cap of 20.
- Subscription amount reflects the billable-count rule of Section 8.1 (including the deactivate-after-payroll case).
- Finalized payroll does not change after a later salary change.
- A prorated calculation is correct for one mid-month joiner and one mid-month leaver.
- Attendance identity violations block finalization.
- Superadmin can suspend and reactivate a company; a suspended company can view history but cannot run payroll.
- Superadmin can reverse a finalized run and the company can re-run that period; the reversed run remains visible and watermarked.

---

# 36. MVP Success Metrics

## Activation

Percentage of created companies that complete setup and process their first payroll.

## Time to First Payroll

Target: less than 15 minutes for initial setup for a typical small company.

## Monthly Payroll Time

Target: less than 5 minutes for a company with 1–9 employees once setup is complete.

## Payroll Completion Rate

Percentage of payroll runs started that are successfully finalized.

## Monthly Retention

Percentage of paying companies that return and process payroll in the following month.

## Revenue Metrics

- Monthly recurring revenue
- Average revenue per company
- Revenue per employee
- Churn rate
- Active paying companies

All metrics above are computed from the analytics events required by FR-044.

---

# 37. MVP Scope

## Must Have

- Multi-tenant architecture
- Superadmin (with MFA)
- Company creation and activation
- Company Admin login, forced password change, password reset
- Single Basic plan with per-employee pricing and limits (cap 20)
- Company setup wizard
- Employee management (incl. joining/exit dates, encrypted bank details)
- Salary structures with recurring components, incl. statutory deduction lines (manual amounts)
- Monthly Inputs screen (attendance identity validation, leave, overtime, bonuses, one-time deductions)
- Payroll calculation engine (Section 19 rules: daily rate, proration, mid-month change, rounding)
- Payroll review with defined warnings and blocking errors
- Payroll finalization with full snapshots
- Payroll reversal (Superadmin, audited)
- Payslip PDF generation (snapshot-driven, individual + combined)
- Disbursement tracking (Paid/Unpaid, mode, date)
- Payroll history
- Subscription status, grace period, manual payment recording
- Audit logging
- Analytics events

## Nice to Have

- CSV import employees
- Excel export
- Email payslips
- Dashboard charts
- Salary revision alerts

## Out of Scope for MVP

- Employee self-service portal
- Multiple Company Admin users per company
- Daily/hourly wage calculation
- Recruitment
- Performance management
- Full HRMS
- Advanced attendance (daily registers, devices, biometrics)
- Multi-country payroll and multi-currency
- **Automated** statutory calculation and filing (TDS/PF/ESI/PT amounts are manual lines in MVP)
- Loan/advance balance tracking
- Expense management
- Accounting integration
- Advanced workflow approvals
- AI features
- Mobile apps
- Self-service plan purchase and online payment
- Complex proration (anything beyond Section 19.3)
- Automated dunning/suspension

---

# 38. Future Roadmap

## Version 2

- Employee login and payslip portal
- Leave request workflow
- Email payslips
- Excel/CSV imports
- Bank payment file export
- Daily/hourly wage support (unlocks restaurants and cafes)
- Multiple admins per company
- Loan/advance balance tracking
- More reports
- Self-service subscription payment
- Second plan tier (Pro)

## Version 3

- Automated PF calculation
- Automated ESI calculation
- Professional Tax slabs by state
- TDS calculation
- Form 16
- Statutory reports
- Automated compliance workflows

## Version 4

- Full HRMS modules
- Recruitment
- Employee onboarding
- Document management
- Performance management
- Expense management
- Mobile applications
- Notifications
- Accounting integrations

---

# 39. Product Principles

### Principle 1: Payroll First

Every MVP feature should directly support payroll or SaaS operations.

### Principle 2: Simple for Small Businesses

A company with five employees should not need an HR specialist to operate the system.

### Principle 3: Superadmin Controls the SaaS

The SaaS owner controls company creation, plans, employee limits and subscription status in the MVP.

### Principle 4: Tenant Isolation Is Mandatory

Company data must never cross tenant boundaries.

### Principle 5: Finalized Payroll Is Immutable

Historical payroll must remain accurate and auditable. Corrections happen by reversal and re-run, never by editing.

### Principle 6: Defaults Over Configuration

Provide sensible defaults and only expose configuration where it has real business value. Where this PRD offers a choice (e.g. daily-rate method), it offers exactly two options with a stated default — never an open-ended setting.

### Principle 7: Monthly Workflow Must Be Fast

After initial setup, the recurring workflow should be:

```text
Monthly Inputs (one screen)
      ↓
Calculate
      ↓
Review
      ↓
Finalize
      ↓
Payslips
      ↓
Mark Paid
```

---

# 40. One-Sentence Product Definition

> **miniPayroll is a multi-tenant payroll SaaS where the platform owner creates and manages company accounts and subscriptions, while each company admin manages employees and runs monthly payroll, with pricing based on billable employees.**

---

# 41. Core Product Loop

```text
YOU
↓
Create Company
↓
Assign Plan
↓
Create Admin Login
↓
COMPANY
↓
Add Employees
↓
Configure Salaries
↓
Run Monthly Payroll
↓
Generate Payslips
↓
Mark Salaries Paid
↓
BILLING
↓
Charge Per Billable Employee
↓
Repeat Every Month
```

This loop should remain the center of the product even as future HRMS capabilities are added.
