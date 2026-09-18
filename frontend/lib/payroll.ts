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

export function billingHoldMessage(
  holdPeriod: string,
  year: number,
  month: number,
): string {
  const holdYear = Number(holdPeriod.slice(0, 4));
  const holdMonth = Number(holdPeriod.slice(5, 7));
  return `Pay ${periodLabel(holdYear, holdMonth)} before starting ${periodLabel(year, month)} payroll.`;
}

export function formatRupees(value: number): string {
  return `₹${value.toLocaleString("en-IN")}`;
}

export function statutoryAppliedLabel(
  applied: number,
  computed: number | null | undefined,
): string {
  const computedAmount = computed ?? applied;
  if (applied === computedAmount) {
    return `${formatRupees(applied)} (computed)`;
  }
  return `${formatRupees(applied)} (computed ${formatRupees(computedAmount)})`;
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

export function calendarDaysInMonth(year: number, month: number): number {
  return new Date(Date.UTC(year, month, 0)).getUTCDate();
}

function parseIsoDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) {
    return null;
  }

  return new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
}

export function daysEmployedInPeriod(
  year: number,
  month: number,
  joiningDate: string | null | undefined,
  exitDate: string | null | undefined,
): number | null {
  const joining = parseIsoDate(joiningDate);
  if (!joining) {
    return null;
  }

  const first = new Date(Date.UTC(year, month - 1, 1));
  const last = new Date(Date.UTC(year, month, 0));
  const exit = parseIsoDate(exitDate);
  if (joining > last || (exit && exit < first)) {
    return 0;
  }

  const from = joining > first ? joining : first;
  const to = exit && exit < last ? exit : last;
  return Math.round((to.getTime() - from.getTime()) / 86_400_000) + 1;
}

export function attendanceWithinMonthBounds(
  workingDays: number,
  present: number,
  paidLeave: number,
  unpaidLeave: number,
  year: number,
  month: number,
  joiningDate: string | null | undefined,
  exitDate: string | null | undefined,
): boolean {
  if (present < 0 || paidLeave < 0 || unpaidLeave < 0 || workingDays < 0) {
    return false;
  }

  const calendarDays = calendarDaysInMonth(year, month);
  if (workingDays > calendarDays) {
    return false;
  }

  if (unpaidLeave > workingDays) {
    return false;
  }

  const daysEmployed = daysEmployedInPeriod(year, month, joiningDate, exitDate);
  return daysEmployed == null || unpaidLeave <= daysEmployed;
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
