import { describe, expect, it } from "vitest";
import {
  newSalaryStructureFields,
  salaryStructureError,
  toSalaryStructureInput,
} from "./SalaryStructureEditor";
import { SalaryComponentType, SalaryComponentValueType } from "@/lib/api";

describe("salary structure editor", () => {
  it("starts with a fixed Basic Salary row", () => {
    const fields = newSalaryStructureFields("2026-01-15");

    expect(fields.components).toHaveLength(1);
    expect(fields.components[0]).toMatchObject({
      name: "Basic Salary",
      type: SalaryComponentType.Earning,
      valueType: SalaryComponentValueType.FixedAmount,
    });
  });

  it("rejects a salary structure without a valid Basic Salary", () => {
    const fields = newSalaryStructureFields("2026-01-15");
    fields.components[0].type = SalaryComponentType.Deduction;
    fields.components[0].value = "25000";

    expect(salaryStructureError(fields, "2026-01-15")).toContain("Basic Salary");
  });

  it("serializes valid fixed and percentage components", () => {
    const fields = newSalaryStructureFields("2026-01-15");
    fields.components[0].value = "25000";
    fields.components.push({
      key: "hra",
      name: "HRA",
      type: SalaryComponentType.Earning,
      valueType: SalaryComponentValueType.PercentageOfBasic,
      value: "40",
    });

    expect(salaryStructureError(fields, "2026-01-15")).toBeNull();
    expect(toSalaryStructureInput(fields)).toMatchObject({
      effectiveFrom: "2026-01-15",
      components: [
        { name: "Basic Salary", value: 25000, sortOrder: 0 },
        { name: "HRA", value: 40, sortOrder: 1 },
      ],
    });
  });
});
