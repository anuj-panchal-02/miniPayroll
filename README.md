# miniPayroll

Multi-tenant payroll SaaS for small businesses in India (1–9 salaried employees). This repository is scaffolded from the MVP PRD: Next.js frontend, .NET Web API, SQL Server, EF Core.

## Stack

| Layer | Choice |
|---|---|
| Frontend | Next.js (App Router) + TypeScript |
| Backend | ASP.NET Core Web API |
| Database | SQL Server / LocalDB |
| ORM | EF Core (code-first, `mp_` table prefix) |
| Auth | ASP.NET Core Identity + JWT |

The machine this was scaffolded on has **.NET 10 SDK** (PRD mentions .NET 8). Target framework is `net10.0` so it builds with the installed SDK.

## Solution layout

```text
backend/
  MiniPayroll.slnx
  src/MiniPayroll.Domain          SaaS entities, enums, setup rules, tenant interface
  src/MiniPayroll.Infrastructure  EF Core + Identity, company setup service
  src/MiniPayroll.Api             Auth, companies, company setup, logo storage, seed
  tests/MiniPayroll.Tests
frontend/                         Next.js Superadmin onboarding + company setup wizard
small_business_payroll_saas_prd.md
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
- Superadmin `superadmin@minipayroll.local` / `ChangeMe_Superadmin1!`

API: `http://localhost:5238`  
Health: `GET /health`

### 3. Frontend

```bash
cd frontend
npm run dev
```

App: `http://localhost:3000` (redirects to login).

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

## Included so far

- Superadmin login
- Create company (name + contact), list companies
- Create Company Admin and activate company (API)
- Company Admin company setup wizard (details → payroll settings → review → complete), resumable
  from the persisted step, with logo upload
- Tenant query filters keyed by `CompanyId`
- All Identity and SaaS tables named `mp_Tbl…`
- Company Admin employee list, add, and edit (encrypted bank details, Active headcount limit)
- Effective-dated employee salary structures with recurring earnings and deductions

Payroll calculation is still a later step.

### Employee routes

Same Company Admin + completed-password gate as setup.

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/employees` | List employees, active count, and limit |
| `GET` | `/api/employees/{id}` | Employee detail (decrypted bank fields) |
| `POST` | `/api/employees` | Create employee |
| `PATCH` | `/api/employees/{id}` | Update employee |

Bank account numbers and IFSC codes are encrypted at rest with ASP.NET Data Protection (`dataprotection-keys/` is gitignored). List responses mask the account as `****1234`. The Active headcount cannot exceed the company `EmployeeLimit` (platform cap 50). Incomplete employees can be stored as `Draft` (`saveAsDraft: true`); drafts do not consume an Active seat.

### Salary structure routes

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/employees/{id}/salary-structures` | List dated salary-structure versions |
| `GET` | `/api/employees/{id}/salary-structure?effectiveOn=YYYY-MM-DD` | Get the version effective on a date |
| `POST` | `/api/employees/{id}/salary-structures` | Add an immutable salary revision |

Each active employee requires a dated structure with exactly one fixed `Basic Salary` earning.
Components can be fixed amounts or a percentage of Basic Salary; PF, ESI, Professional Tax,
LWF and custom manual lines are supported. Salary changes create new versions rather than altering history.

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

Backend tests cover the `mp_` table prefix, tenant isolation, setup rules and service behaviour,
setup authorization, and logo storage. Frontend tests cover the wizard pages, the shell, and the API
client.
