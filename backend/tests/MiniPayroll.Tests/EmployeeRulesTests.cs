using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Tests;

public class EmployeeRulesTests
{
    [Fact]
    public void Normalize_trims_values_and_canonicalizes_bank_fields()
    {
        var employee = ValidEmployee();
        employee.EmployeeCode = "  EMP-01 ";
        employee.FullName = "  Ada Lovelace ";
        employee.Phone = " 9876543210 ";
        employee.Email = " ada@example.com ";
        employee.AddressLine1 = " Line 1 ";
        employee.AddressLine2 = "  ";
        employee.City = " Pune ";
        employee.Department = "  ";
        employee.BankAccountNumber = " 1234 5678 9012 ";
        employee.Ifsc = " hdfc0001234 ";
        employee.UpiId = "  ada@upi ";
        employee.EmploymentType = (EmploymentType)99;

        EmployeeRules.Normalize(employee);

        Assert.Equal("EMP-01", employee.EmployeeCode);
        Assert.Equal("Ada Lovelace", employee.FullName);
        Assert.Null(employee.AddressLine2);
        Assert.Null(employee.Department);
        Assert.Equal("123456789012", employee.BankAccountNumber);
        Assert.Equal("HDFC0001234", employee.Ifsc);
        Assert.Equal("ada@upi", employee.UpiId);
        Assert.Equal(EmploymentType.FullTimeMonthly, employee.EmploymentType);
    }

    [Fact]
    public void Valid_employee_passes()
    {
        Assert.True(EmployeeRules.IsValid(ValidEmployee()));
    }

    [Theory]
    [InlineData("E")]
    [InlineData("EMP 01")]
    [InlineData("EMP_01")]
    [InlineData("EMP.01")]
    public void Employee_code_rejects_invalid_format(string code)
    {
        var employee = ValidEmployee();
        employee.EmployeeCode = code;

        Assert.False(EmployeeRules.IsValid(employee));
    }

    [Fact]
    public void Exit_date_cannot_precede_joining_date()
    {
        var employee = ValidEmployee();
        employee.JoiningDate = new DateOnly(2026, 8, 20);
        employee.ExitDate = new DateOnly(2026, 8, 19);

        Assert.False(EmployeeRules.IsValid(employee));
    }

    [Fact]
    public void Exit_date_on_joining_date_is_allowed()
    {
        var employee = ValidEmployee();
        employee.JoiningDate = new DateOnly(2026, 8, 20);
        employee.ExitDate = new DateOnly(2026, 8, 20);

        Assert.True(EmployeeRules.IsValid(employee));
    }

    [Theory]
    [InlineData("HDFC001234")]
    [InlineData("hdfc0001234")]
    [InlineData("HDFC1001234")]
    [InlineData("HD1C0001234")]
    public void Ifsc_must_match_canonical_format(string ifsc)
    {
        var employee = ValidEmployee();
        employee.Ifsc = ifsc;

        Assert.False(EmployeeRules.IsValid(employee));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890123456789")]
    public void Account_number_must_be_9_to_18_digits(string account)
    {
        var employee = ValidEmployee();
        employee.BankAccountNumber = account;

        Assert.False(EmployeeRules.IsValid(employee));
    }

    [Fact]
    public void Overtime_rate_cannot_be_negative()
    {
        var employee = ValidEmployee();
        employee.OvertimeRate = -1;

        Assert.False(EmployeeRules.IsValid(employee));
    }

    [Fact]
    public void MaskAccountNumber_keeps_last_four_digits()
    {
        Assert.Equal("****9012", EmployeeRules.MaskAccountNumber("123456789012"));
        Assert.Equal("****", EmployeeRules.MaskAccountNumber("12"));
    }

    [Fact]
    public void Draft_with_only_identity_passes()
    {
        var employee = new Employee
        {
            EmployeeCode = "EMP-01",
            FullName = "Ada Lovelace",
            Status = EmployeeStatus.Draft,
            EmploymentType = EmploymentType.FullTimeMonthly,
            DraftStep = 1
        };

        Assert.True(EmployeeRules.IsValidDraft(employee));
        Assert.False(EmployeeRules.IsValid(employee));
    }

    [Fact]
    public void Draft_on_salary_step_is_valid()
    {
        var employee = new Employee
        {
            EmployeeCode = "EMP-01",
            FullName = "Ada Lovelace",
            Status = EmployeeStatus.Draft,
            EmploymentType = EmploymentType.FullTimeMonthly,
            DraftStep = 4
        };

        Assert.True(EmployeeRules.IsValidDraft(employee));
    }

    [Fact]
    public void Draft_step_above_salary_is_invalid()
    {
        var employee = new Employee
        {
            EmployeeCode = "EMP-01",
            FullName = "Ada Lovelace",
            Status = EmployeeStatus.Draft,
            EmploymentType = EmploymentType.FullTimeMonthly,
            DraftStep = 5
        };

        Assert.False(EmployeeRules.IsValidDraft(employee));
    }

    [Fact]
    public void ClampDraftStep_keeps_salary_step_and_defaults_out_of_range()
    {
        Assert.Equal(4, EmployeeRules.ClampDraftStep(4));
        Assert.Equal(1, EmployeeRules.ClampDraftStep(0));
    }

    [Fact]
    public void Draft_rejects_invalid_ifsc_when_present()
    {
        var employee = new Employee
        {
            EmployeeCode = "EMP-01",
            FullName = "Ada Lovelace",
            Status = EmployeeStatus.Draft,
            EmploymentType = EmploymentType.FullTimeMonthly,
            Ifsc = "HDFC1001234"
        };

        Assert.False(EmployeeRules.IsValidDraft(employee));
    }

    [Fact]
    public void Complete_employee_still_requires_bank_and_joining_date()
    {
        var employee = ValidEmployee();
        employee.BankAccountNumber = "";
        employee.Ifsc = "";
        employee.JoiningDate = null;

        Assert.False(EmployeeRules.IsValid(employee));
    }

    private static Employee ValidEmployee() => new()
    {
        EmployeeCode = "EMP-01",
        FullName = "Ada Lovelace",
        Phone = "9876543210",
        Email = "ada@example.com",
        AddressLine1 = "Main Road",
        City = "Pune",
        State = "Maharashtra",
        PostalCode = "411001",
        Designation = "Engineer",
        EmploymentType = EmploymentType.FullTimeMonthly,
        JoiningDate = new DateOnly(2026, 1, 15),
        Status = EmployeeStatus.Active,
        BankName = "HDFC Bank",
        BankAccountNumber = "123456789012",
        Ifsc = "HDFC0001234"
    };
}
