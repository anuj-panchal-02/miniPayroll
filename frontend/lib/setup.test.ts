import { describe, expect, it } from "vitest";
import {
  COMPANY_ADMIN_ROLE,
  CompanySetupStep,
  SUPERADMIN_ROLE,
  guardRedirect,
  homePath,
  setupPath,
  type AuthRoutingState,
} from "./setup";

function account(overrides: Partial<AuthRoutingState> = {}): AuthRoutingState {
  return {
    roles: [COMPANY_ADMIN_ROLE],
    mustChangePassword: false,
    isSetupComplete: false,
    setupStep: CompanySetupStep.CompanyDetails,
    ...overrides,
  };
}

describe("setupPath", () => {
  it.each([
    [CompanySetupStep.CompanyDetails, "/app/setup/company"],
    [CompanySetupStep.PayrollSettings, "/app/setup/payroll"],
    [CompanySetupStep.Review, "/app/setup/review"],
    [CompanySetupStep.Complete, "/app"],
  ])("maps setup step %s to %s", (step, expected) => {
    expect(setupPath(step)).toBe(expected);
  });

  it("falls back safely for an unknown setup step", () => {
    expect(setupPath(99)).toBe("/app/setup/company");
  });
});

describe("homePath", () => {
  it("prioritizes a required password change", () => {
    expect(
      homePath(
        account({
          roles: [SUPERADMIN_ROLE],
          mustChangePassword: true,
          isSetupComplete: true,
          setupStep: CompanySetupStep.Complete,
        }),
      ),
    ).toBe("/change-password");
  });

  it("routes a superadmin to the superadmin home", () => {
    expect(homePath(account({ roles: [SUPERADMIN_ROLE] }))).toBe("/superadmin");
  });

  it("routes an incomplete company admin to the saved setup step", () => {
    expect(
      homePath(account({ setupStep: CompanySetupStep.PayrollSettings })),
    ).toBe("/app/setup/payroll");
  });

  it("routes a completed company admin to the app", () => {
    expect(
      homePath(
        account({
          isSetupComplete: true,
          setupStep: CompanySetupStep.Complete,
        }),
      ),
    ).toBe("/app");
  });
});

describe("guardRedirect", () => {
  it("routes missing authentication to login", () => {
    expect(
      guardRedirect(null, {
        requiredRole: COMPANY_ADMIN_ROLE,
      }),
    ).toBe("/login");
  });

  it("prioritizes a required password change over role checks", () => {
    expect(
      guardRedirect(account({ mustChangePassword: true }), {
        requiredRole: SUPERADMIN_ROLE,
      }),
    ).toBe("/change-password");
  });

  it("blocks company admins from superadmin UI", () => {
    expect(
      guardRedirect(
        account({
          isSetupComplete: true,
          setupStep: CompanySetupStep.Complete,
        }),
        { requiredRole: SUPERADMIN_ROLE },
      ),
    ).toBe("/app");
  });

  it("blocks superadmins from company admin UI", () => {
    expect(
      guardRedirect(account({ roles: [SUPERADMIN_ROLE] }), {
        requiredRole: COMPANY_ADMIN_ROLE,
      }),
    ).toBe("/superadmin");
  });

  it("redirects incomplete admins unless setup access is enabled", () => {
    const incomplete = account({ setupStep: CompanySetupStep.Review });

    expect(
      guardRedirect(incomplete, { requiredRole: COMPANY_ADMIN_ROLE }),
    ).toBe("/app/setup/review");
    expect(
      guardRedirect(incomplete, {
        requiredRole: COMPANY_ADMIN_ROLE,
        allowIncompleteSetup: true,
        currentPath: "/app/setup/review",
      }),
    ).toBeNull();
  });

  it("redirects direct navigation beyond the persisted setup step", () => {
    expect(
      guardRedirect(account({ setupStep: CompanySetupStep.CompanyDetails }), {
        requiredRole: COMPANY_ADMIN_ROLE,
        allowIncompleteSetup: true,
        currentPath: "/app/setup/review",
      }),
    ).toBe("/app/setup/company");

    expect(
      guardRedirect(account({ setupStep: CompanySetupStep.PayrollSettings }), {
        requiredRole: COMPANY_ADMIN_ROLE,
        allowIncompleteSetup: true,
        currentPath: "/app/setup/review",
      }),
    ).toBe("/app/setup/payroll");
  });

  it("allows an incomplete admin to return to an earlier step for editing", () => {
    expect(
      guardRedirect(account({ setupStep: CompanySetupStep.Review }), {
        requiredRole: COMPANY_ADMIN_ROLE,
        allowIncompleteSetup: true,
        currentPath: "/app/setup/company",
      }),
    ).toBeNull();
  });

  it("does not allow incomplete admins outside setup pages", () => {
    expect(
      guardRedirect(account(), {
        requiredRole: COMPANY_ADMIN_ROLE,
        allowIncompleteSetup: true,
        currentPath: "/app",
      }),
    ).toBe("/app/setup/company");
  });

  it("allows a completed admin with the required role", () => {
    expect(
      guardRedirect(
        account({
          isSetupComplete: true,
          setupStep: CompanySetupStep.Complete,
        }),
        { requiredRole: COMPANY_ADMIN_ROLE },
      ),
    ).toBeNull();
  });

  it("allows a completed company admin to open employees", () => {
    expect(
      guardRedirect(
        account({
          isSetupComplete: true,
          setupStep: CompanySetupStep.Complete,
        }),
        {
          requiredRole: COMPANY_ADMIN_ROLE,
          currentPath: "/app/employees",
        },
      ),
    ).toBeNull();
  });

  it.each([
    "/app/setup/company",
    "/app/setup/payroll",
    "/app/setup/review",
  ])("redirects a completed company admin away from %s", (currentPath) => {
    expect(
      guardRedirect(
        account({
          isSetupComplete: true,
          setupStep: CompanySetupStep.Complete,
        }),
        {
          requiredRole: COMPANY_ADMIN_ROLE,
          allowIncompleteSetup: true,
          currentPath,
        },
      ),
    ).toBe("/app");
  });
});
