import { describe, expect, it } from "vitest";
import { attendanceBalances, formatRupees, periodLabel, runStatusLabel } from "./payroll";
import { PayrollRunStatus } from "./api";

describe("payroll helpers", () => {
  it("labels periods and rupee amounts", () => {
    expect(periodLabel(2026, 8)).toBe("August 2026");
    expect(formatRupees(28000)).toBe("₹28,000");
    expect(runStatusLabel(PayrollRunStatus.Draft)).toBe("Draft");
  });

  it("checks attendance identity including half days", () => {
    expect(attendanceBalances(26, 24, 1, 1)).toBe(true);
    expect(attendanceBalances(26, 25.5, 0, 0.5)).toBe(true);
    expect(attendanceBalances(26, 24, 1, 0)).toBe(false);
  });
});
