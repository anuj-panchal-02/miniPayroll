import {
  BonusType,
  OneTimeDeductionType,
  PayrollRunStatus,
} from "@/lib/api";

export const MONTH_LABELS = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
] as const;

export function periodLabel(year: number, month: number): string {
  return `${MONTH_LABELS[month - 1] ?? month} ${year}`;
}

export function formatRupees(value: number): string {
  return `₹${value.toLocaleString("en-IN")}`;
}

export function billingFormula(
  billableEmployees: number,
  pricePerEmployee: number,
  amountDue: number,
): string {
  return `${billableEmployees} × ${formatRupees(pricePerEmployee)} = ${formatRupees(amountDue)}`;
}

export function formatDueDate(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }
  return parsed.toLocaleDateString("en-IN", {
    timeZone: "UTC",
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

export function runStatusLabel(status: PayrollRunStatus): string {
  switch (status) {
    case PayrollRunStatus.Draft:
      return "Draft";
    case PayrollRunStatus.Calculated:
      return "Calculated";
    case PayrollRunStatus.Finalized:
      return "Finalized";
    case PayrollRunStatus.Reversed:
      return "Reversed";
    default:
      return "Unknown";
  }
}

export function attendanceBalances(
  workingDays: number,
  present: number,
  paidLeave: number,
  unpaidLeave: number,
): boolean {
  return (
    Math.round(present * 2) + Math.round(paidLeave * 2) + Math.round(unpaidLeave * 2) ===
    Math.round(workingDays * 2)
  );
}

export function parseQuantity(value: string): number | null {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    return null;
  }
  return parsed;
}

export const BONUS_TYPE_OPTIONS: Array<{ value: string; label: string }> = [
  { value: String(BonusType.Festival), label: "Festival bonus" },
  { value: String(BonusType.Performance), label: "Performance bonus" },
  { value: String(BonusType.Attendance), label: "Attendance bonus" },
  { value: String(BonusType.Incentive), label: "Incentive" },
  { value: String(BonusType.Other), label: "Other bonus" },
];

export const DEDUCTION_TYPE_OPTIONS: Array<{ value: string; label: string }> = [
  { value: String(OneTimeDeductionType.AdvanceRecovery), label: "Advance recovery" },
  { value: String(OneTimeDeductionType.LoanInstallment), label: "Loan installment" },
  { value: String(OneTimeDeductionType.Tds), label: "TDS" },
  { value: String(OneTimeDeductionType.Other), label: "Other deduction" },
];
