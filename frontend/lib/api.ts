import { CompanySetupStep } from "./setup";

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

export type CreateAdminResponse = {
  userId: string;
  email: string;
  temporaryPassword: string;
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

export function createCompanyAdmin(id: string, email: string): Promise<CreateAdminResponse> {
  return api<CreateAdminResponse>(`/api/companies/${id}/admin`, {
    method: "POST",
    body: JSON.stringify({ email }),
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

export const EmploymentType = {
  FullTimeMonthly: 0,
} as const;
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
