import { HARD_EMPLOYEE_CAP } from "@/lib/platform";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function companyNameError(value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return "Enter a company name.";
  }
  if (trimmed.length < 2) {
    return "Company name must be at least 2 characters.";
  }
  if (trimmed.length > 200) {
    return "Company name must be 200 characters or fewer.";
  }
  return null;
}

export function emailError(
  value: string,
  messages: { empty: string; invalid: string },
): string | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return messages.empty;
  }
  if (!EMAIL_PATTERN.test(trimmed)) {
    return messages.invalid;
  }
  return null;
}

export function employeeLimitError(raw: string): string | null {
  const message = `Employee limit must be between 1 and ${HARD_EMPLOYEE_CAP}.`;
  const trimmed = raw.trim();
  if (!trimmed) {
    return message;
  }
  const n = Number(trimmed);
  if (!Number.isInteger(n) || n < 1 || n > HARD_EMPLOYEE_CAP) {
    return message;
  }
  return null;
}

export function currentPasswordError(value: string): string | null {
  if (!value) {
    return "Enter your current password.";
  }
  return null;
}

export function newPasswordError(value: string): string | null {
  if (!value) {
    return "Enter a new password.";
  }
  const hasLower = /[a-z]/.test(value);
  const hasUpper = /[A-Z]/.test(value);
  const hasDigit = /\d/.test(value);
  const hasSymbol = /[^A-Za-z0-9]/.test(value);
  if (value.length < 10 || !hasLower || !hasUpper || !hasDigit || !hasSymbol) {
    return "Use at least 10 characters with upper and lower case, a number, and a symbol.";
  }
  return null;
}

export function confirmPasswordError(value: string, newPassword: string): string | null {
  if (value !== newPassword) {
    return "Passwords do not match.";
  }
  return null;
}

export type CompanySetupFields = {
  name: string;
  contactEmail: string;
  contactPhone: string;
  addressLine1: string;
  addressLine2: string;
  city: string;
  state: string;
  postalCode: string;
};

export type CompanySetupField = keyof CompanySetupFields;
export type CompanySetupErrors = Partial<Record<CompanySetupField, string>>;

const COMPANY_FIELD_RULES: Record<
  Exclude<CompanySetupField, "contactEmail" | "addressLine2">,
  { label: string; max: number; empty: string }
> = {
  name: { label: "Company name", max: 200, empty: "Enter a company name." },
  contactPhone: { label: "Phone number", max: 30, empty: "Enter a phone number." },
  addressLine1: { label: "Address", max: 200, empty: "Enter an address." },
  city: { label: "City", max: 100, empty: "Enter a city." },
  state: { label: "State", max: 100, empty: "Enter a state." },
  postalCode: { label: "Postal code", max: 20, empty: "Enter a postal code." },
};

function requiredSetupFieldError(
  value: string,
  rule: { label: string; max: number; empty: string },
): string | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return rule.empty;
  }
  if (trimmed.length > rule.max) {
    return `${rule.label} must be ${rule.max} characters or fewer.`;
  }
  return null;
}

export function companySetupErrors(values: CompanySetupFields): CompanySetupErrors {
  const errors: CompanySetupErrors = {};

  for (const field of Object.keys(COMPANY_FIELD_RULES) as Array<
    keyof typeof COMPANY_FIELD_RULES
  >) {
    const error = requiredSetupFieldError(values[field], COMPANY_FIELD_RULES[field]);
    if (error) {
      errors[field] = error;
    }
  }

  const contactEmailError = emailError(values.contactEmail, {
    empty: "Enter a contact email.",
    invalid: "Enter a valid email address.",
  });
  if (contactEmailError) {
    errors.contactEmail = contactEmailError;
  } else if (values.contactEmail.trim().length > 256) {
    errors.contactEmail = "Contact email must be 256 characters or fewer.";
  }

  if (values.addressLine2.trim().length > 200) {
    errors.addressLine2 = "Address line 2 must be 200 characters or fewer.";
  }

  return errors;
}

const ALLOWED_LOGO_TYPES = new Set(["image/png", "image/jpeg", "image/webp"]);
export const MAX_LOGO_BYTES = 2 * 1024 * 1024;

export function logoFileError(file: File): string | null {
  if (!ALLOWED_LOGO_TYPES.has(file.type)) {
    return "Choose a PNG, JPEG, or WebP image.";
  }
  if (file.size > MAX_LOGO_BYTES) {
    return "Company logo must be 2 MB or smaller.";
  }
  return null;
}

export function workingDaysError(raw: string): string | null {
  const value = Number(raw);
  if (!raw.trim() || !Number.isInteger(value) || value < 1 || value > 31) {
    return "Working days must be a whole number from 1 to 31.";
  }
  return null;
}

export function weeklyOffDaysError(days: string[]): string | null {
  return days.length === 0 ? "Select at least one weekly off day." : null;
}

export type EmployeeFields = {
  employeeCode: string;
  fullName: string;
  dateOfBirth: string;
  phone: string;
  email: string;
  addressLine1: string;
  addressLine2: string;
  city: string;
  state: string;
  postalCode: string;
  designation: string;
  department: string;
  joiningDate: string;
  exitDate: string;
  bankName: string;
  bankAccountNumber: string;
  ifsc: string;
  upiId: string;
  overtimeRate: string;
};
export type EmployeeField = keyof EmployeeFields;
export type EmployeeErrors = Partial<Record<EmployeeField, string>>;

const EMPLOYEE_CODE_PATTERN = /^[A-Za-z0-9-]+$/;
const IFSC_PATTERN = /^[A-Z]{4}0[A-Z0-9]{6}$/;

export const PERSONAL_FIELDS: EmployeeField[] = [
  "employeeCode",
  "fullName",
  "email",
  "phone",
  "dateOfBirth",
  "addressLine1",
  "addressLine2",
  "city",
  "state",
  "postalCode",
  "designation",
  "department",
  "joiningDate",
  "exitDate",
];

export const BANK_FIELDS: EmployeeField[] = [
  "bankName",
  "bankAccountNumber",
  "ifsc",
  "upiId",
];

export const PAYROLL_FIELDS: EmployeeField[] = ["overtimeRate"];

function requiredLengthError(
  value: string,
  empty: string,
  max: number,
  label: string,
): string | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return empty;
  }
  if (trimmed.length > max) {
    return `${label} must be ${max} characters or fewer.`;
  }
  return null;
}

function optionalMaxError(value: string, max: number, message: string): string | null {
  return value.trim().length > max ? message : null;
}

function employeeCodeError(value: string): string | null {
  const code = value.trim();
  if (!code) {
    return "Enter an employee ID.";
  }
  if (code.length < 2 || code.length > 32 || !EMPLOYEE_CODE_PATTERN.test(code)) {
    return "Employee ID must be 2–32 letters, numbers, or hyphens.";
  }
  return null;
}

function accountError(value: string, required: boolean): string | null {
  const accountDigits = value.replace(/\D/g, "");
  if (!accountDigits) {
    return required ? "Enter a bank account number." : null;
  }
  if (accountDigits.length < 9 || accountDigits.length > 18) {
    return "Account number must be 9 to 18 digits.";
  }
  return null;
}

function ifscError(value: string, required: boolean): string | null {
  const ifsc = value.trim().toUpperCase();
  if (!ifsc) {
    return required ? "Enter an IFSC code." : null;
  }
  return IFSC_PATTERN.test(ifsc) ? null : "Enter a valid IFSC code.";
}

export function employeePersonalErrors(values: EmployeeFields): EmployeeErrors {
  const errors: EmployeeErrors = {};
  const codeErr = employeeCodeError(values.employeeCode);
  if (codeErr) errors.employeeCode = codeErr;
  const nameError = requiredLengthError(values.fullName, "Enter a full name.", 200, "Full name");
  if (nameError) errors.fullName = nameError;
  const emailErr = emailError(values.email, {
    empty: "Enter an email.",
    invalid: "Enter a valid email address.",
  });
  if (emailErr) errors.email = emailErr;
  const phoneError = requiredLengthError(values.phone, "Enter a phone number.", 30, "Phone number");
  if (phoneError) errors.phone = phoneError;
  const addressError = requiredLengthError(values.addressLine1, "Enter an address.", 200, "Address");
  if (addressError) errors.addressLine1 = addressError;
  const line2 = optionalMaxError(
    values.addressLine2,
    200,
    "Address line 2 must be 200 characters or fewer.",
  );
  if (line2) errors.addressLine2 = line2;
  const cityError = requiredLengthError(values.city, "Enter a city.", 100, "City");
  if (cityError) errors.city = cityError;
  const stateError = requiredLengthError(values.state, "Enter a state.", 100, "State");
  if (stateError) errors.state = stateError;
  const postalError = requiredLengthError(values.postalCode, "Enter a postal code.", 20, "Postal code");
  if (postalError) errors.postalCode = postalError;
  const designationError = requiredLengthError(
    values.designation,
    "Enter a designation.",
    100,
    "Designation",
  );
  if (designationError) errors.designation = designationError;
  const department = optionalMaxError(
    values.department,
    100,
    "Department must be 100 characters or fewer.",
  );
  if (department) errors.department = department;
  if (!values.joiningDate) {
    errors.joiningDate = "Enter a joining date.";
  }
  if (values.exitDate && values.joiningDate && values.exitDate < values.joiningDate) {
    errors.exitDate = "Exit date cannot be before the joining date.";
  }
  return errors;
}

export function employeeBankErrors(values: EmployeeFields): EmployeeErrors {
  const errors: EmployeeErrors = {};
  const bankError = requiredLengthError(values.bankName, "Enter a bank name.", 100, "Bank name");
  if (bankError) errors.bankName = bankError;
  const account = accountError(values.bankAccountNumber, true);
  if (account) errors.bankAccountNumber = account;
  const ifsc = ifscError(values.ifsc, true);
  if (ifsc) errors.ifsc = ifsc;
  const upi = optionalMaxError(values.upiId, 100, "UPI ID must be 100 characters or fewer.");
  if (upi) errors.upiId = upi;
  return errors;
}

export function employeePayrollErrors(values: EmployeeFields): EmployeeErrors {
  const errors: EmployeeErrors = {};
  if (values.overtimeRate.trim()) {
    const rate = Number(values.overtimeRate);
    if (!Number.isFinite(rate) || rate < 0) {
      errors.overtimeRate = "Overtime rate cannot be negative.";
    }
  }
  return errors;
}

export function employeeDraftErrors(values: EmployeeFields): EmployeeErrors {
  const errors: EmployeeErrors = {};
  const codeErr = employeeCodeError(values.employeeCode);
  if (codeErr) errors.employeeCode = codeErr;
  const nameError = requiredLengthError(values.fullName, "Enter a full name.", 200, "Full name");
  if (nameError) errors.fullName = nameError;
  if (values.email.trim()) {
    const emailErr = emailError(values.email, {
      empty: "Enter an email.",
      invalid: "Enter a valid email address.",
    });
    if (emailErr) errors.email = emailErr;
  }
  const phone = optionalMaxError(values.phone, 30, "Phone number must be 30 characters or fewer.");
  if (phone) errors.phone = phone;
  const address = optionalMaxError(values.addressLine1, 200, "Address must be 200 characters or fewer.");
  if (address) errors.addressLine1 = address;
  const line2 = optionalMaxError(
    values.addressLine2,
    200,
    "Address line 2 must be 200 characters or fewer.",
  );
  if (line2) errors.addressLine2 = line2;
  const city = optionalMaxError(values.city, 100, "City must be 100 characters or fewer.");
  if (city) errors.city = city;
  const state = optionalMaxError(values.state, 100, "State must be 100 characters or fewer.");
  if (state) errors.state = state;
  const postal = optionalMaxError(
    values.postalCode,
    20,
    "Postal code must be 20 characters or fewer.",
  );
  if (postal) errors.postalCode = postal;
  const designation = optionalMaxError(
    values.designation,
    100,
    "Designation must be 100 characters or fewer.",
  );
  if (designation) errors.designation = designation;
  const department = optionalMaxError(
    values.department,
    100,
    "Department must be 100 characters or fewer.",
  );
  if (department) errors.department = department;
  if (values.exitDate && values.joiningDate && values.exitDate < values.joiningDate) {
    errors.exitDate = "Exit date cannot be before the joining date.";
  }
  const bank = optionalMaxError(values.bankName, 100, "Bank name must be 100 characters or fewer.");
  if (bank) errors.bankName = bank;
  const account = accountError(values.bankAccountNumber, false);
  if (account) errors.bankAccountNumber = account;
  const ifsc = ifscError(values.ifsc, false);
  if (ifsc) errors.ifsc = ifsc;
  const upi = optionalMaxError(values.upiId, 100, "UPI ID must be 100 characters or fewer.");
  if (upi) errors.upiId = upi;
  Object.assign(errors, employeePayrollErrors(values));
  return errors;
}

export function employeeErrors(values: EmployeeFields): EmployeeErrors {
  return {
    ...employeePersonalErrors(values),
    ...employeeBankErrors(values),
    ...employeePayrollErrors(values),
  };
}
