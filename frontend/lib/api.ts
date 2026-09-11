import { CompanySetupStep } from "./setup";
import { clearSession } from "./session";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5238";
const TOKEN_KEY = "mp_token";

export function safeApiAssetUrl(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }

  try {
    const apiUrl = new URL(API_URL);
    const assetUrl = new URL(value, apiUrl);
    if (
      !["http:", "https:"].includes(apiUrl.protocol) ||
      !["http:", "https:"].includes(assetUrl.protocol) ||
      assetUrl.origin !== apiUrl.origin ||
      assetUrl.username ||
      assetUrl.password
    ) {
      return null;
    }
    return assetUrl.href;
  } catch {
    return null;
  }
}

export type AuthResponse = {
  token: string;
  email: string;
  roles: string[];
  companyId: string | null;
  mustChangePassword: boolean;
  requiresMfaEnrollment: boolean;
  isSetupComplete: boolean;
  setupStep: CompanySetupStep;
};

export type CompanyListItem = {
  id: string;
  name: string;
  contactEmail: string;
  status: string;
  employeeLimit: number;
  isSetupComplete: boolean;
  activatedAt: string | null;
  hasAdmin: boolean;
};

export type CompanyDetail = {
  id: string;
  name: string;
  contactEmail: string;
  contactPhone: string | null;
  status: string;
  employeeLimit: number;
  planName: string;
  isSetupComplete: boolean;
  activatedAt: string | null;
  hasAdmin: boolean;
  adminEmail: string | null;
};

export type PlatformLimitsResponse = {
  minEmployeeLimit: number;
  hardEmployeeCap: number;
  defaultEmployeeLimit: number;
  defaultPlanName: string;
  currencyCode: string;
};

export type PlatformStateItem = {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  sortOrder: number;
};

export type PlatformCityItem = {
  id: string;
  stateId: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
};

export type CreateAdminResponse = {
  userId: string;
  email: string;
};

export type MeResponse = {
  id: string;
  email: string;
  roles: string[];
  companyId: string | null;
  mustChangePassword: boolean;
  twoFactorEnabled: boolean;
  isSetupComplete: boolean;
  setupStep: CompanySetupStep;
};

export enum DailyRateMethod {
  CalendarDays = 0,
  FixedThirty = 1,
}

export type CompanySetup = {
  name: string;
  contactEmail: string;
  contactPhone: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  logoUrl: string | null;
  payrollCycle: string;
  dailyRateMethod: keyof typeof DailyRateMethod;
  workingDaysPerMonth: number;
  weeklyOffDays: string[];
  setupStep: CompanySetupStep;
  isSetupComplete: boolean;
  pfApplicable: boolean;
  pfUseWageCeiling: boolean;
  esiApplicable: boolean;
  pfEstablishmentCode: string | null;
  esiCode: string | null;
};

export type CompanyDetailsInput = {
  name: string;
  contactEmail: string;
  contactPhone: string;
  addressLine1: string;
  addressLine2?: string | null;
  city: string;
  state: string;
  postalCode: string;
};

export type PayrollSettingsInput = {
  dailyRateMethod: DailyRateMethod;
  workingDaysPerMonth: number;
  weeklyOffDays: string[];
  pfApplicable: boolean;
  pfUseWageCeiling: boolean;
  esiApplicable: boolean;
  pfEstablishmentCode?: string | null;
  esiCode?: string | null;
};

type SetupStepWireValue = number | string;
type AuthResponseWire = Omit<AuthResponse, "setupStep"> & {
  setupStep: SetupStepWireValue;
};
type MeResponseWire = Omit<MeResponse, "setupStep"> & {
  setupStep: SetupStepWireValue;
};
type CompanySetupWire = Omit<CompanySetup, "setupStep"> & {
  setupStep: SetupStepWireValue;
};

function parseSetupStep(value: SetupStepWireValue): CompanySetupStep {
  switch (value) {
    case CompanySetupStep.CompanyDetails:
    case "CompanyDetails":
      return CompanySetupStep.CompanyDetails;
    case CompanySetupStep.PayrollSettings:
    case "PayrollSettings":
      return CompanySetupStep.PayrollSettings;
    case CompanySetupStep.Review:
    case "Review":
      return CompanySetupStep.Review;
    case CompanySetupStep.Complete:
    case "Complete":
      return CompanySetupStep.Complete;
    default:
      return CompanySetupStep.CompanyDetails;
  }
}

function normalizeSetup<T extends { setupStep: SetupStepWireValue }>(
  response: T,
): Omit<T, "setupStep"> & { setupStep: CompanySetupStep } {
  const normalized = {
    ...response,
    setupStep: parseSetupStep(response.setupStep),
  };
  if ("logoUrl" in response) {
    return {
      ...normalized,
      logoUrl: safeApiAssetUrl(
        typeof response.logoUrl === "string" ? response.logoUrl : null,
      ),
    };
  }
  return normalized;
}

export function getToken(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string | null): void {
  if (token) {
    localStorage.setItem(TOKEN_KEY, token);
  } else {
    localStorage.removeItem(TOKEN_KEY);
  }
  clearSession();
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const headers = new Headers(init?.headers);
  if (
    !headers.has("Content-Type") &&
    init?.body &&
    !(init.body instanceof FormData)
  ) {
    headers.set("Content-Type", "application/json");
  }
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${API_URL}${path}`, { ...init, headers });
  if (response.status === 204) {
    return undefined as T;
  }

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const message =
      payload?.error ??
      payload?.title ??
      (Array.isArray(payload?.errors) ? payload.errors.join(", ") : null) ??
      `Request failed (${response.status})`;
    throw new Error(message);
  }

  return payload as T;
}

export async function login(email: string, password: string): Promise<AuthResponse> {
  const response = await api<AuthResponseWire>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  return normalizeSetup(response);
}

export async function getMe(): Promise<MeResponse> {
  return normalizeSetup(await api<MeResponseWire>("/api/auth/me"));
}

export function changePassword(
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  return api<void>("/api/auth/change-password", {
    method: "POST",
    body: JSON.stringify({ currentPassword, newPassword }),
  });
}

export function getPlatformLimits(): Promise<PlatformLimitsResponse> {
  return api<PlatformLimitsResponse>("/api/platform", { method: "GET" });
}

export function listPlatformStates(includeInactive = false): Promise<PlatformStateItem[]> {
  const query = includeInactive ? "?includeInactive=true" : "";
  return api<PlatformStateItem[]>(`/api/platform/states${query}`, { method: "GET" });
}

export function listPlatformCities(
  stateId: string,
  includeInactive = false,
): Promise<PlatformCityItem[]> {
  const params = new URLSearchParams({ stateId });
  if (includeInactive) {
    params.set("includeInactive", "true");
  }
  return api<PlatformCityItem[]>(`/api/platform/cities?${params.toString()}`, { method: "GET" });
}

export function createPlatformState(input: {
  name: string;
  code: string;
}): Promise<PlatformStateItem> {
  return api<PlatformStateItem>("/api/platform/states", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updatePlatformState(
  id: string,
  input: { name?: string; code?: string; isActive?: boolean },
): Promise<PlatformStateItem> {
  return api<PlatformStateItem>(`/api/platform/states/${id}`, {
    method: "PATCH",
    body: JSON.stringify(input),
  });
}

export function createPlatformCity(input: {
  stateId: string;
  name: string;
}): Promise<PlatformCityItem> {
  return api<PlatformCityItem>("/api/platform/cities", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updatePlatformCity(
  id: string,
  input: { name?: string; isActive?: boolean },
): Promise<PlatformCityItem> {
  return api<PlatformCityItem>(`/api/platform/cities/${id}`, {
    method: "PATCH",
    body: JSON.stringify(input),
  });
}

export function listCompanies(): Promise<CompanyListItem[]> {
  return api<CompanyListItem[]>("/api/companies");
}

export function createCompany(input: {
  name: string;
  contactEmail: string;
  contactPhone?: string;
  employeeLimit: number;
}): Promise<CompanyDetail> {
  return api<CompanyDetail>("/api/companies", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function getCompany(id: string): Promise<CompanyDetail> {
  return api<CompanyDetail>(`/api/companies/${id}`);
}

export function createCompanyAdmin(
  id: string,
  email: string,
  temporaryPassword: string,
): Promise<CreateAdminResponse> {
  return api<CreateAdminResponse>(`/api/companies/${id}/admin`, {
    method: "POST",
    body: JSON.stringify({ email, temporaryPassword }),
  });
}

export function activateCompany(id: string): Promise<CompanyDetail> {
  return api<CompanyDetail>(`/api/companies/${id}/activate`, { method: "POST" });
}

export function updateCompanyLimit(id: string, employeeLimit: number): Promise<CompanyDetail> {
  return api<CompanyDetail>(`/api/companies/${id}/limit`, {
    method: "PATCH",
    body: JSON.stringify({ employeeLimit }),
  });
}

export async function getCompanySetup(): Promise<CompanySetup> {
  return normalizeSetup(
    await api<CompanySetupWire>("/api/company/setup", { method: "GET" }),
  );
}

export async function updateCompanyDetails(
  input: CompanyDetailsInput,
): Promise<CompanySetup> {
  return normalizeSetup(
    await api<CompanySetupWire>("/api/company/setup/details", {
      method: "PATCH",
      body: JSON.stringify(input),
    }),
  );
}

export async function updatePayrollSettings(
  input: PayrollSettingsInput,
): Promise<CompanySetup> {
  return normalizeSetup(
    await api<CompanySetupWire>("/api/company/setup/payroll-settings", {
      method: "PATCH",
      body: JSON.stringify(input),
    }),
  );
}

export async function updateCompletedPayrollSettings(
  input: PayrollSettingsInput,
): Promise<CompanySetup> {
  return normalizeSetup(
    await api<CompanySetupWire>("/api/company/payroll-settings", {
      method: "PATCH",
      body: JSON.stringify(input),
    }),
  );
}

export async function uploadCompanyLogo(file: File): Promise<CompanySetup> {
  const body = new FormData();
  body.append("file", file);
  return normalizeSetup(
    await api<CompanySetupWire>("/api/company/setup/logo", {
      method: "POST",
      body,
    }),
  );
}

export async function completeCompanySetup(): Promise<CompanySetup> {
  return normalizeSetup(
    await api<CompanySetupWire>("/api/company/setup/complete", {
      method: "POST",
    }),
  );
}

export const EmployeeStatus = {
  Active: 0,
  Inactive: 1,
  Draft: 2,
} as const;
export type EmployeeStatus = (typeof EmployeeStatus)[keyof typeof EmployeeStatus];

export const Gender = {
  Male: 0,
  Female: 1,
} as const;
export type Gender = (typeof Gender)[keyof typeof Gender];

export const StatutoryKind = {
  PfEmployee: 0,
  EsiEmployee: 1,
  ProfessionalTax: 2,
  LwfEmployee: 3,
} as const;
export type StatutoryKind = (typeof StatutoryKind)[keyof typeof StatutoryKind];
export type EmploymentType = (typeof EmploymentType)[keyof typeof EmploymentType];

export const SalaryComponentType = {
  Earning: 0,
  Deduction: 1,
} as const;
export type SalaryComponentType = (typeof SalaryComponentType)[keyof typeof SalaryComponentType];

export const SalaryComponentValueType = {
  FixedAmount: 0,
  PercentageOfBasic: 1,
} as const;
export type SalaryComponentValueType =
  (typeof SalaryComponentValueType)[keyof typeof SalaryComponentValueType];

export type SalaryStructureComponentInput = {
  name: string;
  type: SalaryComponentType;
  valueType: SalaryComponentValueType;
  value: number;
  sortOrder: number;
};

export type SalaryStructureInput = {
  effectiveFrom: string;
  components: SalaryStructureComponentInput[];
};

export type SalaryStructureComponentDetail = SalaryStructureComponentInput & {
  id: string;
};

export type SalaryStructureDetail = {
  id: string;
  employeeId: string;
  effectiveFrom: string;
  createdAt: string;
  components: SalaryStructureComponentDetail[];
  recurringEarnings: number;
  recurringDeductions: number;
};

export type EmployeeListItem = {
  id: string;
  employeeCode: string;
  fullName: string;
  designation: string;
  department: string | null;
  status: EmployeeStatus;
  joiningDate: string | null;
  maskedAccountNumber: string;
};

export type EmployeeListState = {
  employees: EmployeeListItem[];
  activeCount: number;
  employeeLimit: number;
};

export type EmployeeDetail = {
  id: string;
  employeeCode: string;
  fullName: string;
  dateOfBirth: string | null;
  phone: string;
  email: string;
  addressLine1: string;
  addressLine2: string | null;
  city: string;
  state: string;
  postalCode: string;
  designation: string;
  department: string | null;
  employmentType: EmploymentType;
  joiningDate: string | null;
  exitDate: string | null;
  status: EmployeeStatus;
  draftStep: number | null;
  bankName: string;
  bankAccountNumber: string;
  maskedAccountNumber: string;
  ifsc: string;
  upiId: string | null;
  overtimeRate: number | null;
  gender: Gender | null;
  pfCovered: boolean;
  esiCovered: boolean;
  uan: string | null;
  pfNumber: string | null;
  esiNumber: string | null;
};

export type EmployeeInput = {
  employeeCode: string;
  fullName: string;
  dateOfBirth?: string | null;
  phone: string;
  email: string;
  addressLine1: string;
  addressLine2?: string | null;
  city: string;
  state: string;
  postalCode: string;
  designation: string;
  department?: string | null;
  joiningDate?: string | null;
  exitDate?: string | null;
  status?: EmployeeStatus;
  bankName: string;
  bankAccountNumber: string;
  ifsc: string;
  upiId?: string | null;
  overtimeRate?: number | null;
  gender?: Gender | null;
  pfCovered?: boolean;
  esiCovered?: boolean;
  uan?: string | null;
  pfNumber?: string | null;
  esiNumber?: string | null;
  saveAsDraft?: boolean;
  draftStep?: number | null;
  salaryStructure?: SalaryStructureInput | null;
};

export function listEmployees(): Promise<EmployeeListState> {
  return api<EmployeeListState>("/api/employees", { method: "GET" });
}

export function getEmployee(id: string): Promise<EmployeeDetail> {
  return api<EmployeeDetail>(`/api/employees/${id}`, { method: "GET" });
}

export function createEmployee(input: EmployeeInput): Promise<EmployeeDetail> {
  return api<EmployeeDetail>("/api/employees", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateEmployee(
  id: string,
  input: EmployeeInput,
): Promise<EmployeeDetail> {
  return api<EmployeeDetail>(`/api/employees/${id}`, {
    method: "PATCH",
    body: JSON.stringify(input),
  });
}

export function listSalaryStructures(employeeId: string): Promise<SalaryStructureDetail[]> {
  return api<SalaryStructureDetail[]>(`/api/employees/${employeeId}/salary-structures`);
}

export function getEffectiveSalaryStructure(
  employeeId: string,
  effectiveOn?: string,
): Promise<SalaryStructureDetail> {
  const query = effectiveOn ? `?effectiveOn=${encodeURIComponent(effectiveOn)}` : "";
  return api<SalaryStructureDetail>(`/api/employees/${employeeId}/salary-structure${query}`);
}

export function createSalaryStructure(
  employeeId: string,
  input: SalaryStructureInput,
): Promise<SalaryStructureDetail> {
  return api<SalaryStructureDetail>(`/api/employees/${employeeId}/salary-structures`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export const PayrollRunStatus = {
  Draft: 0,
  Calculated: 1,
  Finalized: 2,
  Reversed: 3,
} as const;
export type PayrollRunStatus = (typeof PayrollRunStatus)[keyof typeof PayrollRunStatus];

export const BonusType = {
  Festival: 0,
  Performance: 1,
  Attendance: 2,
  Incentive: 3,
  Other: 4,
} as const;
export type BonusType = (typeof BonusType)[keyof typeof BonusType];

export const OneTimeDeductionType = {
  AdvanceRecovery: 0,
  LoanInstallment: 1,
  Tds: 2,
  Other: 3,
} as const;
export type OneTimeDeductionType =
  (typeof OneTimeDeductionType)[keyof typeof OneTimeDeductionType];

export const PayrollLineKind = {
  RecurringEarning: 0,
  Overtime: 1,
  Bonus: 2,
  RecurringDeduction: 3,
  UnpaidLeave: 4,
  OneTimeDeduction: 5,
  Statutory: 6,
} as const;
export type PayrollLineKind = (typeof PayrollLineKind)[keyof typeof PayrollLineKind];

export const SalaryPaymentStatus = {
  Unpaid: 0,
  Paid: 1,
} as const;
export type SalaryPaymentStatus =
  (typeof SalaryPaymentStatus)[keyof typeof SalaryPaymentStatus];

export const SalaryPaymentMode = {
  Bank: 0,
  Upi: 1,
  Cash: 2,
} as const;
export type SalaryPaymentMode = (typeof SalaryPaymentMode)[keyof typeof SalaryPaymentMode];

export type PayrollAttendanceDetail = {
  employeeId: string;
  workingDays: number;
  present: number;
  paidLeave: number;
  unpaidLeave: number;
};

export type PayrollOvertimeDetail = {
  id: string;
  employeeId: string;
  hours: number;
  rate: number | null;
  notes: string | null;
};

export type PayrollBonusDetail = {
  id: string;
  employeeId: string;
  type: BonusType;
  amount: number;
  notes: string | null;
};

export type PayrollDeductionDetail = {
  id: string;
  employeeId: string;
  type: OneTimeDeductionType;
  amount: number;
  notes: string | null;
};

export type PayrollRosterEmployee = {
  employeeId: string;
  employeeCode: string;
  fullName: string;
  status: EmployeeStatus;
  joiningDate: string | null;
  exitDate: string | null;
  hasStructure: boolean;
  overtimeRate: number | null;
  attendance: PayrollAttendanceDetail | null;
  overtime: PayrollOvertimeDetail[];
  bonuses: PayrollBonusDetail[];
  deductions: PayrollDeductionDetail[];
};

export type PayrollPeriodRunSummary = {
  id: string;
  status: PayrollRunStatus;
  dailyRateMethod: DailyRateMethod;
  createdAt: string;
  calculatedAt: string | null;
  finalizedAt: string | null;
};

export type PayrollLineDetail = {
  name: string;
  kind: PayrollLineKind;
  amount: number;
  sortOrder: number;
  computedAmount?: number | null;
  statutoryKind?: StatutoryKind | null;
};

export type PayrollEmployeeDetail = {
  employeeId: string;
  employeeCode: string;
  fullName: string;
  designation: string;
  daysEmployed: number;
  dailyRate: number;
  grossEarnings: number;
  totalDeductions: number;
  netSalary: number;
  earnings: PayrollLineDetail[];
  deductions: PayrollLineDetail[];
  warnings: string[];
  errors: string[];
  paymentStatus: SalaryPaymentStatus;
  paymentMode: SalaryPaymentMode | null;
  paidOn: string | null;
  paymentReference: string | null;
  employerPf?: number;
  employerEsi?: number;
};

export type PayrollTotals = {
  grossEarnings: number;
  totalDeductions: number;
  netSalary: number;
  employeeCount: number;
  warningCount: number;
  errorCount: number;
};

export type PayrollPeriodDetail = {
  year: number;
  month: number;
  workingDaysPerMonth: number;
  run: PayrollPeriodRunSummary | null;
  employees: PayrollRosterEmployee[];
  results: PayrollEmployeeDetail[];
  totals: PayrollTotals | null;
};

export type PayrollRunDetail = {
  id: string;
  year: number;
  month: number;
  runStatus: PayrollRunStatus;
  dailyRateMethod: DailyRateMethod;
  createdAt: string;
  calculatedAt: string | null;
  finalizedAt: string | null;
  employees: PayrollEmployeeDetail[];
};

export type PayrollInputsPayload = {
  attendance: PayrollAttendanceDetail[];
  overtime: Array<{
    employeeId: string;
    hours: number;
    rate: number | null;
    notes: string | null;
  }>;
  bonuses: Array<{
    employeeId: string;
    type: BonusType;
    amount: number;
    notes: string | null;
  }>;
  deductions: Array<{
    employeeId: string;
    type: OneTimeDeductionType;
    amount: number;
    notes: string | null;
  }>;
};

export function getPayrollPeriod(year: number, month: number): Promise<PayrollPeriodDetail> {
  return api<PayrollPeriodDetail>(`/api/payroll/${year}/${month}`, { method: "GET" });
}

export function createPayrollRun(year: number, month: number): Promise<PayrollRunDetail> {
  return api<PayrollRunDetail>(`/api/payroll/${year}/${month}/run`, { method: "POST" });
}

export function savePayrollInputs(
  runId: string,
  input: PayrollInputsPayload,
): Promise<PayrollPeriodDetail> {
  return api<PayrollPeriodDetail>(`/api/payroll/runs/${runId}/inputs`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function calculatePayroll(year: number, month: number): Promise<PayrollRunDetail> {
  return api<PayrollRunDetail>(`/api/payroll/${year}/${month}/calculate`, { method: "POST" });
}

export function setStatutoryOverrides(
  runId: string,
  employeeId: string,
  overrides: Array<{ kind: StatutoryKind; amount: number }>,
): Promise<PayrollRunDetail> {
  return api<PayrollRunDetail>(
    `/api/payroll/runs/${runId}/employees/${employeeId}/statutory-overrides`,
    {
      method: "PUT",
      body: JSON.stringify({ overrides }),
    },
  );
}

export function finalizePayroll(runId: string): Promise<PayrollRunDetail> {
  return api<PayrollRunDetail>(`/api/payroll/runs/${runId}/finalize`, { method: "POST" });
}

export type PayrollHistoryItem = {
  id: string;
  year: number;
  month: number;
  status: PayrollRunStatus;
  employeeCount: number;
  grossEarnings: number;
  totalDeductions: number;
  netSalary: number;
  createdAt: string;
  calculatedAt: string | null;
  finalizedAt: string | null;
};

export type PayrollPaymentPayload = {
  paymentStatus: SalaryPaymentStatus;
  paymentMode: SalaryPaymentMode | null;
  paidOn: string | null;
  paymentReference: string | null;
};

export function listPayrollRuns(): Promise<PayrollHistoryItem[]> {
  return api<PayrollHistoryItem[]>("/api/payroll/runs", { method: "GET" });
}

export function updatePayrollPayment(
  runId: string,
  employeeId: string,
  input: PayrollPaymentPayload,
): Promise<PayrollPeriodDetail> {
  return api<PayrollPeriodDetail>(
    `/api/payroll/runs/${runId}/employees/${employeeId}/payment`,
    { method: "PUT", body: JSON.stringify(input) },
  );
}

export function listCompanyPayrollRuns(companyId: string): Promise<PayrollHistoryItem[]> {
  return api<PayrollHistoryItem[]>(`/api/companies/${companyId}/payroll-runs`, {
    method: "GET",
  });
}

export function reversePayrollRun(
  companyId: string,
  runId: string,
  reason: string,
): Promise<PayrollRunDetail> {
  return api<PayrollRunDetail>(`/api/companies/${companyId}/payroll-runs/${runId}/reverse`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
}

export const BillableSource = {
  FinalizedPayroll: 0,
  ActiveHeadcount: 1,
} as const;

export type BillableSource = (typeof BillableSource)[keyof typeof BillableSource];

export type BillingPaymentItem = {
  id: string;
  amount: number;
  paidOn: string;
  paymentMode: string;
  invoiceGstReference: string | null;
  recordedAt: string;
};

export type BillingPeriodSummary = {
  billingPeriod: string;
  year: number;
  month: number;
  billableEmployees: number;
  billableSource: BillableSource;
  pricePerEmployee: number;
  amountDue: number;
  prorated: boolean;
  isEstimated: boolean;
  dueDate: string;
  isOverdue: boolean;
  isPastGrace: boolean;
  paidAmount: number;
  remaining: number;
  payments: BillingPaymentItem[];
};

export type CompanyBilling = {
  planName: string;
  pricePerEmployee: number;
  gracePeriodDays: number;
  periods: BillingPeriodSummary[];
};

export type RecordCompanyPaymentInput = {
  billingPeriod: string;
  amount: number;
  paidOn: string;
  paymentMode: string;
  invoiceGstReference?: string | null;
};

export function getCompanyBilling(companyId: string): Promise<CompanyBilling> {
  return api<CompanyBilling>(`/api/companies/${companyId}/billing`, { method: "GET" });
}

export function getWorkspaceBilling(): Promise<CompanyBilling> {
  return api<CompanyBilling>("/api/company/billing", { method: "GET" });
}

export type PlatformPlan = {
  id: string;
  name: string;
  pricePerEmployee: number;
};

export function getPlatformPlan(): Promise<PlatformPlan> {
  return api<PlatformPlan>("/api/platform/plan", { method: "GET" });
}

export function updatePlatformPlan(pricePerEmployee: number): Promise<PlatformPlan> {
  return api<PlatformPlan>("/api/platform/plan", {
    method: "PATCH",
    body: JSON.stringify({ pricePerEmployee }),
  });
}

export function recordCompanyPayment(
  companyId: string,
  input: RecordCompanyPaymentInput,
): Promise<CompanyBilling> {
  return api<CompanyBilling>(`/api/companies/${companyId}/payments`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export async function downloadPayrollPayslip(
  runId: string,
  employeeId: string,
): Promise<void> {
  await saveDownload(`/api/payroll/runs/${runId}/payslips/${employeeId}`);
}

export async function downloadAllPayrollPayslips(runId: string): Promise<void> {
  await saveDownload(`/api/payroll/runs/${runId}/payslips`);
}

async function saveDownload(path: string): Promise<void> {
  const token = getToken();
  const headers = new Headers();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  const response = await fetch(`${API_URL}${path}`, { headers });
  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    const message =
      payload?.error ??
      payload?.title ??
      `Request failed (${response.status})`;
    throw new Error(message);
  }
  const disposition = response.headers.get("Content-Disposition") ?? "";
  const match = /filename\*?=(?:UTF-8''|")?([^\";]+)/i.exec(disposition);
  const fileName = match?.[1]?.replaceAll('"', "") ?? "payslip.pdf";
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = decodeURIComponent(fileName);
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
