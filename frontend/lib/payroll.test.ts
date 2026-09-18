import { describe, expect, it } from "vitest";
import {
  attendanceBalances,
  attendanceWithinMonthBounds,
  billingFormula,
  calendarDaysInMonth,
  daysEmployedInPeriod,
  formatRupees,
  periodLabel,
  runStatusLabel,
  statutoryAppliedLabel,
} from "./payroll";
import { PayrollRunStatus } from "./api";

describe("payroll helpers", () => {
  it("labels periods and rupee amounts", () => {
    expect(periodLabel(2026, 8)).toBe("August 2026");
    expect(formatRupees(28000)).toBe("₹28,000");
    expect(statutoryAppliedLabel(1800, 1800)).toBe("₹1,800 (computed)");
    expect(statutoryAppliedLabel(0, 1800)).toBe("₹0 (computed ₹1,800)");
    expect(billingFormula(2, 49, 98)).toBe("2 × ₹49 = ₹98");
    expect(runStatusLabel(PayrollRunStatus.Draft)).toBe("Draft");
  });

  it("checks attendance identity including half days", () => {
    expect(attendanceBalances(26, 24, 1, 1)).toBe(true);
    expect(attendanceBalances(26, 25.5, 0, 0.5)).toBe(true);
    expect(attendanceBalances(26, 24, 1, 0)).toBe(false);
  });

  it("caps attendance by calendar days and days employed", () => {
    expect(calendarDaysInMonth(2026, 8)).toBe(31);
    expect(calendarDaysInMonth(2026, 2)).toBe(28);
    expect(daysEmployedInPeriod(2026, 8, "2025-01-01", null)).toBe(31);
    expect(daysEmployedInPeriod(2026, 8, "2026-08-28", null)).toBe(4);
    expect(daysEmployedInPeriod(2026, 8, "2025-01-01", "2026-08-05")).toBe(5);
    expect(daysEmployedInPeriod(2026, 2, "2025-01-01", null)).toBe(28);
    expect(daysEmployedInPeriod(2026, 8, null, null)).toBeNull();
    expect(attendanceWithinMonthBounds(30, 30, 0, 0, 2026, 2, "2025-01-01", null)).toBe(false);
    expect(attendanceWithinMonthBounds(32, 32, 0, 0, 2026, 8, "2025-01-01", null)).toBe(false);
    expect(attendanceWithinMonthBounds(26, 0, 0, 27, 2026, 8, "2025-01-01", null)).toBe(false);
    expect(attendanceWithinMonthBounds(26, 16, 0, 10, 2026, 8, "2026-08-28", null)).toBe(false);
    expect(attendanceWithinMonthBounds(26, 20, 0, 0, 2026, 8, "2025-01-01", null)).toBe(true);
  });
});
