using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Domain.Payroll;

public static class PayrollCalculationEngine
{
    public static PayrollEmployeeResult Calculate(
        PayrollEmployeeInput input,
        IStatutoryRuleProvider? rules = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var provider = rules ?? ConfiguredStatutoryRuleProvider.Instance;
        var validation = PayrollInputValidator.Validate(input, provider);
        if (validation.Errors.Count > 0)
        {
            return new PayrollEmployeeResult([], 0m, 0m, 0m, 0, 0m, validation.Errors, []);
        }

        var warnings = new List<string>();
        var period = input.Period;
        var attendance = input.Attendance;
        var daysEmployed = validation.DaysEmployed;
        var calendarDays = period.CalendarDays;

        var earnings = RecurringEarningsCalculator.Calculate(
            validation.Structure,
            validation.Basic!.Value,
            daysEmployed,
            calendarDays,
            input.DailyRateMethod);
        if (earnings.IgnoredStatutoryOnStructure)
        {
            warnings.Add(PayrollCalculationMessages.IgnoredStatutoryStructure);
        }

        var lines = new List<PayrollResultLine>();
        lines.AddRange(earnings.Lines);
        lines.AddRange(OvertimeCalculator.Calculate(validation.Overtime));
        lines.AddRange(BonusCalculator.Calculate(input.Bonuses));

        var unpaid = UnpaidLeaveCalculator.Calculate(attendance.UnpaidLeave, earnings.DailyRate);
        lines.AddRange(unpaid.Lines);
        lines.AddRange(OneTimeDeductionCalculator.Calculate(input.OneTimeDeductions));

        var employerPf = 0m;
        var employerEsi = 0m;
        if (input.Statutory is { } policy)
        {
            var bases = StatutoryWageBaseCalculator.Calculate(
                earnings.EarningStructure, lines, unpaid.Amount);
            var statutory = StatutoryCalculator.Calculate(
                policy,
                period.LastDay,
                period.Month,
                bases.PfWages,
                bases.EsiWages,
                bases.PtWages,
                input.Overrides,
                provider);

            foreach (var line in statutory.Where(item => item.Applied != 0 || item.Computed != 0))
            {
                lines.Add(new PayrollResultLine(
                    StatutoryLabels.Name(line.Kind),
                    SalaryComponentType.Deduction,
                    PayrollLineKind.Statutory,
                    line.Applied,
                    line.Kind,
                    line.Computed));
                if (line.Applied != line.Computed)
                {
                    warnings.Add(PayrollCalculationMessages.StatutoryOverrideApplied);
                }
            }

            employerPf = statutory.Single(item => item.Kind == StatutoryKind.PfEmployee).EmployerAmount;
            employerEsi = statutory.Single(item => item.Kind == StatutoryKind.EsiEmployee).EmployerAmount;
            var esiRule = provider.EsiFor(period.LastDay);
            if (policy.EsiApplicable && policy.EsiCovered && bases.EsiWages > esiRule.EligibilityCeiling)
            {
                warnings.Add(PayrollCalculationMessages.EsiWagesAboveThreshold);
            }
        }

        return NetSalaryAssembler.Assemble(
            input,
            lines,
            daysEmployed,
            earnings.DailyRate,
            warnings,
            employerPf,
            employerEsi);
    }
}
