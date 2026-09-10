export const DEFAULT_PLAN_NAME = "Basic";

/** Keep in sync with MiniPayroll.Domain.Constants.PlatformLimits. Raise HardEmployeeCap there (and here as fallback) when the product supports more employees. */
export const MIN_EMPLOYEE_LIMIT = 1;
export const HARD_EMPLOYEE_CAP = 50;
export const DEFAULT_EMPLOYEE_LIMIT = HARD_EMPLOYEE_CAP;

export type PlatformLimits = {
  minEmployeeLimit: number;
  hardEmployeeCap: number;
  defaultEmployeeLimit: number;
  defaultPlanName: string;
  currencyCode: string;
};

export const FALLBACK_PLATFORM_LIMITS: PlatformLimits = {
  minEmployeeLimit: MIN_EMPLOYEE_LIMIT,
  hardEmployeeCap: HARD_EMPLOYEE_CAP,
  defaultEmployeeLimit: DEFAULT_EMPLOYEE_LIMIT,
  defaultPlanName: DEFAULT_PLAN_NAME,
  currencyCode: "INR",
};

