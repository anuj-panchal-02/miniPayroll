using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Tests;

public class PayrollSourceFingerprintTests
{
    [Fact]
    public void Hash_is_stable_for_the_same_snapshot()
    {
        var snapshot = Sample();
        Assert.Equal(PayrollSourceFingerprint.Hash(snapshot), PayrollSourceFingerprint.Hash(snapshot));
    }

    [Fact]
    public void Hash_changes_when_a_component_value_changes()
    {
        var original = Sample();
        var changed = original with
        {
            Employees =
            [
                original.Employees[0] with
                {
                    Structure =
                    [
                        original.Employees[0].Structure[0] with { Value = 21000m }
                    ]
                }
            ]
        };

        Assert.NotEqual(PayrollSourceFingerprint.Hash(original), PayrollSourceFingerprint.Hash(changed));
    }

    private static PayrollSourceSnapshot Sample() => new(
        DailyRateMethod.CalendarDays,
        new DateOnly(2026, 8, 31),
        new DateOnly(2014, 9, 1),
        new DateOnly(2019, 7, 1),
        0.12m,
        0.12m,
        15000m,
        0.0075m,
        0.0325m,
        21000m,
        new PayrollCompanyStatutorySource(true, true, false, "Maharashtra"),
        [
            new PayrollEmployeeSource(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "EMP-01",
                "Ada Lovelace",
                "Engineer",
                new DateOnly(2026, 1, 1),
                null,
                EmployeeStatus.Active,
                Gender.Female,
                true,
                false,
                100m,
                new DateOnly(2026, 1, 1),
                [
                    new PayrollStructureComponentSource(
                        "Basic Salary",
                        SalaryComponentType.Earning,
                        SalaryComponentValueType.FixedAmount,
                        20000m,
                        0,
                        SalaryComponentKind.Basic)
                ],
                new PayrollAttendanceSource(26m, 26m, 0m, 0m),
                [],
                [],
                [],
                [])
        ]);
}
