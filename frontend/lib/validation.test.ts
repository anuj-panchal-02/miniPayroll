import { describe, expect, it } from "vitest";
import { HARD_EMPLOYEE_CAP, MIN_EMPLOYEE_LIMIT } from "./platform";
import {
  PASSWORD_STRENGTH_MAX,
  companySetupErrors,
  employeeDraftErrors,
  employeeErrors,
  employeeLimitError,
  logoFileError,
  newPasswordError,
  passwordStrength,
  weeklyOffDaysError,
  workingDaysError,
} from "./validation";

describe("company setup validation", () => {
  it("reports every required company field", () => {
    expect(
      companySetupErrors({
        name: "",
        contactEmail: "",
        contactPhone: "",
        addressLine1: "",
        addressLine2: "",
        city: "",
        state: "",
        postalCode: "",
      }),
    ).toEqual({
      name: "Enter a company name.",
      contactEmail: "Enter a contact email.",
      contactPhone: "Enter a phone number.",
      addressLine1: "Enter an address.",
      city: "Enter a city.",
      state: "Enter a state.",
      postalCode: "Enter a postal code.",
    });
  });

  it("matches backend length and email rules", () => {
    const errors = companySetupErrors({
      name: "A".repeat(201),
      contactEmail: "not-an-email",
      contactPhone: "1".repeat(31),
      addressLine1: "A".repeat(201),
      addressLine2: "A".repeat(201),
      city: "A".repeat(101),
      state: "A".repeat(101),
      postalCode: "1".repeat(21),
    });

    expect(errors.name).toContain("200");
    expect(errors.contactEmail).toBe("Enter a valid email address.");
    expect(errors.contactPhone).toContain("30");
    expect(errors.addressLine2).toContain("200");
    expect(errors.city).toContain("100");
    expect(errors.state).toContain("100");
    expect(errors.postalCode).toContain("20");
  });
});

describe("logo validation", () => {
  it("accepts PNG, JPEG, and WebP files up to 2 MB", () => {
    expect(logoFileError(new File(["ok"], "logo.png", { type: "image/png" }))).toBeNull();
    expect(logoFileError(new File(["ok"], "logo.jpg", { type: "image/jpeg" }))).toBeNull();
    expect(logoFileError(new File(["ok"], "logo.webp", { type: "image/webp" }))).toBeNull();
  });

  it("rejects unsupported and oversized files", () => {
    expect(logoFileError(new File(["x"], "logo.gif", { type: "image/gif" }))).toContain(
      "PNG, JPEG, or WebP",
    );
    expect(
      logoFileError(
        new File([new Uint8Array(2 * 1024 * 1024 + 1)], "large.png", {
          type: "image/png",
        }),
      ),
    ).toContain("2 MB");
  });
});

describe("payroll validation", () => {
  it("requires an integer from 1 through 31", () => {
    expect(workingDaysError("0")).toBeTruthy();
    expect(workingDaysError("31")).toBeNull();
    expect(workingDaysError("2.5")).toBeTruthy();
  });

  it("requires at least one weekly off day", () => {
    expect(weeklyOffDaysError([])).toBe("Select at least one weekly off day.");
    expect(weeklyOffDaysError(["Sunday"])).toBeNull();
  });
});

describe("password strength", () => {
  it("only calls a password strong when newPasswordError accepts it", () => {
    const strong = "Design.dey123!";
    expect(newPasswordError(strong)).toBeNull();

    const rated = passwordStrength(strong);
    expect(rated.tone).toBe("success");
    expect(rated.score).toBe(PASSWORD_STRENGTH_MAX);
    expect(rated.label).toBe("Password strong");
    expect(rated.advice).toBeNull();
  });

  it("caps a short password below the top of the meter even with every character class", () => {
    const short = "Aa1!";
    expect(newPasswordError(short)).toBeTruthy();
    expect(passwordStrength(short).score).toBe(2);
    expect(passwordStrength(short).tone).toBe("error");
  });

  it("rates a long password missing one character class as improvable, not strong", () => {
    const rated = passwordStrength("Designdey123");
    expect(rated.score).toBe(3);
    expect(rated.tone).toBe("neutral");
    expect(rated.advice).toBeTruthy();
  });

  it("treats an empty password as the weakest state", () => {
    const rated = passwordStrength("");
    expect(rated.score).toBe(0);
    expect(rated.tone).toBe("error");
  });
});

describe("employee limit validation", () => {
  it("accepts the configured min and hard cap", () => {
    expect(employeeLimitError(String(MIN_EMPLOYEE_LIMIT))).toBeNull();
    expect(employeeLimitError(String(HARD_EMPLOYEE_CAP))).toBeNull();
  });

  it("rejects values outside the configured range", () => {
    expect(employeeLimitError(String(MIN_EMPLOYEE_LIMIT - 1))).toBe(
      `Employee limit must be between ${MIN_EMPLOYEE_LIMIT} and ${HARD_EMPLOYEE_CAP}.`,
    );
    expect(employeeLimitError(String(HARD_EMPLOYEE_CAP + 1))).toBe(
      `Employee limit must be between ${MIN_EMPLOYEE_LIMIT} and ${HARD_EMPLOYEE_CAP}.`,
    );
  });

  it("uses a caller-supplied range so a higher platform cap does not require rewriting this check", () => {
    expect(employeeLimitError("10", { min: 1, max: 10 })).toBeNull();
    expect(employeeLimitError("11", { min: 1, max: 10 })).toBe(
      "Employee limit must be between 1 and 10.",
    );
  });
});

describe("employee validation", () => {
  const valid = {
    employeeCode: "EMP-01",
    fullName: "Ada Lovelace",
    dateOfBirth: "",
    phone: "9876543210",
    email: "ada@example.com",
    addressLine1: "Main Road",
    addressLine2: "",
    city: "Pune",
    state: "Maharashtra",
    postalCode: "411001",
    designation: "Engineer",
    department: "",
    joiningDate: "2026-01-15",
    exitDate: "",
    bankName: "HDFC Bank",
    bankAccountNumber: "123456789012",
    ifsc: "HDFC0001234",
    upiId: "",
    overtimeRate: "",
  };

  it("accepts a valid employee", () => {
    expect(employeeErrors(valid)).toEqual({});
  });

  it("rejects an invalid employee ID and IFSC", () => {
    const errors = employeeErrors({
      ...valid,
      employeeCode: "E 1",
      ifsc: "HDFC1001234",
    });
    expect(errors.employeeCode).toBeTruthy();
    expect(errors.ifsc).toBe("Enter a valid IFSC code.");
  });

  it("asks for empty required bank fields", () => {
    const errors = employeeErrors({
      ...valid,
      joiningDate: "",
      bankAccountNumber: "",
      ifsc: "",
    });
    expect(errors.joiningDate).toBe("Enter a joining date.");
    expect(errors.bankAccountNumber).toBe("Enter a bank account number.");
    expect(errors.ifsc).toBe("Enter an IFSC code.");
  });

  it("lets a draft keep optional fields empty", () => {
    expect(
      employeeDraftErrors({
        ...valid,
        phone: "",
        email: "",
        addressLine1: "",
        city: "",
        state: "",
        postalCode: "",
        designation: "",
        joiningDate: "",
        bankName: "",
        bankAccountNumber: "",
        ifsc: "",
      }),
    ).toEqual({});
  });

  it("rejects an exit date before joining", () => {
    expect(
      employeeErrors({
        ...valid,
        joiningDate: "2026-08-20",
        exitDate: "2026-08-19",
      }).exitDate,
    ).toBe("Exit date cannot be before the joining date.");
  });
});
