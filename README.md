# miniPayroll

Payroll for small companies in India.

miniPayroll runs the month — people, attendance, calculation, locked figures, and payslips — without an HR, leave, or tax suite. It is built for companies with **1–50 salaried employees**. Superadmin provisions the account; the company admin signs in and does the period.

## Product

Company admins work a single monthly loop:

1. **People and pay** — salaried employees, encrypted bank details, and dated salary structures (one Basic Salary line, plus earnings and deductions including PF, ESI, Professional Tax, and LWF).
2. **Monthly inputs** — working days, present days, leave, and one-time overtime, bonus, or deductions for the period.
3. **Calculate, review, lock** — draft the run, check gross and net, then finalize so the figures cannot drift.
4. **Payslips and payment** — download payslip PDFs from the locked run and record payment against those figures.

Closed months stay on a payroll history ledger. Superadmin can reverse a finalized run when a company needs the period opened again.

There is no self-serve signup. Existing users sign in at `/login`. The public site at `/` is the product introduction.

## Stack

| Layer | Choice |
|---|---|
| Frontend | Next.js (App Router) + TypeScript |
| Backend | ASP.NET Core Web API |
| Database | SQL Server / LocalDB |
| ORM | EF Core (code-first, `mp_` table prefix) |
| Auth | ASP.NET Core Identity + JWT |

Target framework is `net10.0`.

## Solution layout

```text
backend/
  MiniPayroll.slnx
  src/MiniPayroll.Domain
  src/MiniPayroll.Infrastructure
  src/MiniPayroll.Api
  tests/MiniPayroll.Tests
frontend/                         Next.js app, Superadmin, company workspace, landing
```

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- SQL Server LocalDB (`MSSQLLocalDB`) or another SQL Server instance

## Run locally

### 1. Database

Default connection string uses LocalDB:

```text
Server=(localdb)\mssqllocaldb;Database=MiniPayroll;Trusted_Connection=True;TrustServerCertificate=True
```

Change `backend/src/MiniPayroll.Api/appsettings.Development.json` if you want a named SQL Server instance instead.

### 2. API

```bash
cd backend
dotnet tool restore
dotnet test
dotnet run --project src/MiniPayroll.Api --launch-profile http
```

On first Development run the API applies migrations and seeds:

- Basic plan (₹49 / employee, Superadmin sets the limit, cap 50)
- Superadmin `superadmin@minipayroll.local` if `Seed:SuperadminPassword` is set (user-secrets or env)

From `backend/src/MiniPayroll.Api`:

```bash
dotnet user-secrets set "Jwt:Key" "<at least 32 characters>"
dotnet user-secrets set "Seed:SuperadminPassword" "<Identity-valid password>"
```

Production Superadmin bootstrap also requires `Seed:AllowBootstrap=true`. See `.env.example` for environment variable names.

API: `http://localhost:5238`  
Health: `GET /health`

### 3. Frontend

```bash
cd frontend
npm run dev
```

App: `http://localhost:3000` — product landing; **Log in** goes to `/login`.

## File storage

Company logos are written to disk and served as static files under `/uploads`. Configure them with
the `FileStorage` section of `backend/src/MiniPayroll.Api/appsettings.json`:

| Key | Default | Meaning |
|---|---|---|
| `FileStorage:RootPath` | `uploads` | Logo root, resolved against the API content root |
| `FileStorage:MaxLogoBytes` | `2097152` | Per-file limit (2 MB); also caps the multipart body |

Only PNG, JPEG, and WebP content is accepted (detected from the file signature, not the extension).
Stored logos are served publicly so the frontend `<img>` can render them without a bearer token, and
responses carry `X-Content-Type-Options: nosniff`. The upload directory is gitignored.

## Platform

- Superadmin creates companies, Company Admin users, and plan limits
- Company Admin completes a setup wizard (details → payroll settings → review), then manages employees and payroll
- Tenant query filters keyed by `CompanyId`
- Identity and SaaS tables named `mp_Tbl…`
- Active headcount cannot exceed the company `EmployeeLimit` (platform cap 50)

To support more than 50 employees later, raise `HardEmployeeCap` in `backend/src/MiniPayroll.Domain/Constants/PlatformLimits.cs`. Keep the fallback in `frontend/lib/platform.ts` in sync; Superadmin screens also load live values from `GET /api/platform`.

### Employee routes

Same Company Admin + completed-password gate as setup.

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/employees` | List employees, active count, and limit |
| `GET` | `/api/employees/{id}` | Employee detail (decrypted bank fields) |
| `POST` | `/api/employees` | Create employee |
| `PATCH` | `/api/employees/{id}` | Update employee |

Bank account numbers and IFSC codes are encrypted at rest with ASP.NET Data Protection (`dataprotection-keys/` is gitignored). List responses mask the account as `****1234`. Incomplete employees can be stored as `Draft` (`saveAsDraft: true`); drafts do not consume an Active seat.

### Salary structure routes

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/employees/{id}/salary-structures` | List dated salary-structure versions |
| `GET` | `/api/employees/{id}/salary-structure?effectiveOn=YYYY-MM-DD` | Get the version effective on a date |
| `POST` | `/api/employees/{id}/salary-structures` | Add an immutable salary revision |

Each active employee requires a dated structure with exactly one fixed `Basic Salary` earning.
Components can be fixed amounts or a percentage of Basic Salary. Salary changes create new versions rather than altering history.

### Payroll routes

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/payroll/runs` | List payroll runs for the company |
| `GET` | `/api/payroll/{year}/{month}` | Period detail and current run |
| `POST` | `/api/payroll/{year}/{month}/run` | Create a run for the period |
| `POST` | `/api/payroll/{year}/{month}/calculate` | Draft or recalculate |
| `PUT` | `/api/payroll/runs/{runId}/inputs` | Save monthly inputs |
| `POST` | `/api/payroll/runs/{runId}/finalize` | Lock the run |
| `PUT` | `/api/payroll/runs/{runId}/employees/{employeeId}/payment` | Record or clear payment |
| `GET` | `/api/payroll/runs/{runId}/payslips` | Combined payslip PDF |
| `GET` | `/api/payroll/runs/{runId}/payslips/{employeeId}` | Employee payslip PDF |
| `GET` | `/api/companies/{id}/payroll-runs` | Superadmin: runs for a company |
| `POST` | `/api/companies/{id}/payroll-runs/{runId}/reverse` | Superadmin: reverse a run |

### Company setup routes

All routes require the `CompanyAdmin` role and a completed password change (checked against the
database on every request, not the JWT claim).

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/company/setup` | Current setup state and step |
| `PATCH` | `/api/company/setup/details` | Company name, contact, address |
| `PATCH` | `/api/company/setup/payroll-settings` | Daily rate method, working days, weekly offs |
| `POST` | `/api/company/setup/logo` | Multipart logo upload (`file` field) |
| `POST` | `/api/company/setup/complete` | Marks setup complete and writes the audit row |

Completion is guarded by an optimistic concurrency token: a request that loses the race gets `409`
instead of writing a second audit row.

## Tests

Backend:

```bash
dotnet test backend/MiniPayroll.slnx
```

Frontend:

```bash
cd frontend
npm test
npm run lint
```
