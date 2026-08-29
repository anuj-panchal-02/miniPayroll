export const SUPERADMIN_ROLE = "Superadmin";
export const COMPANY_ADMIN_ROLE = "CompanyAdmin";

export const LOGIN_PATH = "/login";
export const CHANGE_PASSWORD_PATH = "/change-password";
export const SUPERADMIN_PATH = "/superadmin";
export const COMPANY_APP_PATH = "/app";
export const COMPANY_SETUP_PATH = "/app/setup/company";
export const PAYROLL_SETUP_PATH = "/app/setup/payroll";
export const REVIEW_SETUP_PATH = "/app/setup/review";

export enum CompanySetupStep {
  CompanyDetails = 0,
  PayrollSettings = 1,
  Review = 2,
  Complete = 3,
}

export type AuthRoutingState = {
  roles: string[];
  mustChangePassword: boolean;
  isSetupComplete: boolean;
  setupStep: CompanySetupStep;
};

export type RouteGuardOptions = {
  requiredRole: typeof SUPERADMIN_ROLE | typeof COMPANY_ADMIN_ROLE;
  allowIncompleteSetup?: boolean;
  currentPath?: string;
};

export function setupPath(step: number): string {
  switch (step) {
    case CompanySetupStep.CompanyDetails:
      return COMPANY_SETUP_PATH;
    case CompanySetupStep.PayrollSettings:
      return PAYROLL_SETUP_PATH;
    case CompanySetupStep.Review:
      return REVIEW_SETUP_PATH;
    case CompanySetupStep.Complete:
      return COMPANY_APP_PATH;
    default:
      return COMPANY_SETUP_PATH;
  }
}

export function homePath(account: AuthRoutingState): string {
  if (account.mustChangePassword) {
    return CHANGE_PASSWORD_PATH;
  }

  if (account.roles.includes(SUPERADMIN_ROLE)) {
    return SUPERADMIN_PATH;
  }

  if (!account.roles.includes(COMPANY_ADMIN_ROLE)) {
    return LOGIN_PATH;
  }

  return account.isSetupComplete
    ? COMPANY_APP_PATH
    : setupPath(account.setupStep);
}

export function isSetupPath(path: string | undefined): boolean {
  return (
    path === COMPANY_SETUP_PATH ||
    path === PAYROLL_SETUP_PATH ||
    path === REVIEW_SETUP_PATH
  );
}

function setupStepForPath(path: string | undefined): CompanySetupStep | null {
  switch (path) {
    case COMPANY_SETUP_PATH:
      return CompanySetupStep.CompanyDetails;
    case PAYROLL_SETUP_PATH:
      return CompanySetupStep.PayrollSettings;
    case REVIEW_SETUP_PATH:
      return CompanySetupStep.Review;
    default:
      return null;
  }
}

function isSetupRoute(path: string | undefined): boolean {
  return path === "/app/setup" || path?.startsWith("/app/setup/") === true;
}

export function guardRedirect(
  account: AuthRoutingState | null,
  options: RouteGuardOptions,
): string | null {
  if (!account) {
    return LOGIN_PATH;
  }

  const destination = homePath(account);
  if (account.mustChangePassword) {
    return destination;
  }

  if (!account.roles.includes(options.requiredRole)) {
    return destination;
  }

  if (
    options.requiredRole === COMPANY_ADMIN_ROLE &&
    account.isSetupComplete &&
    isSetupRoute(options.currentPath)
  ) {
    return COMPANY_APP_PATH;
  }

  const requestedSetupStep = setupStepForPath(options.currentPath);
  if (
    options.requiredRole === COMPANY_ADMIN_ROLE &&
    !account.isSetupComplete &&
    options.allowIncompleteSetup &&
    requestedSetupStep !== null &&
    requestedSetupStep > account.setupStep
  ) {
    return setupPath(account.setupStep);
  }

  if (
    options.requiredRole === COMPANY_ADMIN_ROLE &&
    !account.isSetupComplete &&
    (!options.allowIncompleteSetup || !isSetupPath(options.currentPath))
  ) {
    return destination;
  }

  return null;
}
