using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Tests;

public class CompanySetupRulesTests
{
    [Fact]
    public void Company_starts_at_company_details_step()
    {
        var company = new Company();

        Assert.Equal(CompanySetupStep.CompanyDetails, company.SetupStep);
    }

    [Fact]
    public void NormalizeCompanyDetails_trims_values_and_clears_blank_optional_address()
    {
        var company = ValidCompany();
        company.Name = "  ABC Traders  ";
        company.ContactEmail = "  owner@example.com ";
        company.ContactPhone = "  1234567890 ";
        company.AddressLine1 = "  Main Road ";
        company.AddressLine2 = "   ";
        company.City = "  Pune ";
        company.State = "  Maharashtra ";
        company.PostalCode = "  411001 ";

        CompanySetupRules.NormalizeCompanyDetails(company);

        Assert.Equal("ABC Traders", company.Name);
        Assert.Equal("owner@example.com", company.ContactEmail);
        Assert.Equal("1234567890", company.ContactPhone);
        Assert.Equal("Main Road", company.AddressLine1);
        Assert.Null(company.AddressLine2);
        Assert.Equal("Pune", company.City);
        Assert.Equal("Maharashtra", company.State);
        Assert.Equal("411001", company.PostalCode);
    }

    [Theory]
    [InlineData(nameof(Company.Name))]
    [InlineData(nameof(Company.ContactEmail))]
    [InlineData(nameof(Company.ContactPhone))]
    [InlineData(nameof(Company.AddressLine1))]
    [InlineData(nameof(Company.City))]
    [InlineData(nameof(Company.State))]
    [InlineData(nameof(Company.PostalCode))]
    public void Company_details_require_all_mandatory_fields(string propertyName)
    {
        var company = ValidCompany();
        typeof(Company).GetProperty(propertyName)!.SetValue(company, "   ");

        Assert.False(CompanySetupRules.HasValidCompanyDetails(company));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("owner@")]
    [InlineData("@example.com")]
    [InlineData("owner @example.com")]
    public void Company_details_require_a_valid_email(string email)
    {
        var company = ValidCompany();
        company.ContactEmail = email;

        Assert.False(CompanySetupRules.HasValidCompanyDetails(company));
    }

    [Fact]
    public void Company_details_enforce_persisted_field_lengths_after_trimming()
    {
        var company = ValidCompany();
        company.Name = $"  {new string('n', CompanySetupRules.NameMaxLength)}  ";
        company.ContactEmail = $"{new string('a', 243)}@example.com";
        company.ContactPhone = new string('p', CompanySetupRules.PhoneMaxLength);
        company.AddressLine1 = new string('a', CompanySetupRules.AddressLineMaxLength);
        company.AddressLine2 = new string('b', CompanySetupRules.AddressLineMaxLength);
        company.City = new string('c', CompanySetupRules.CityMaxLength);
        company.State = new string('s', CompanySetupRules.StateMaxLength);
        company.PostalCode = new string('z', CompanySetupRules.PostalCodeMaxLength);

        Assert.True(CompanySetupRules.HasValidCompanyDetails(company));

        company.AddressLine2 += "x";

        Assert.False(CompanySetupRules.HasValidCompanyDetails(company));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Payroll_settings_require_working_days_between_one_and_thirty_one(int workingDays)
    {
        var company = ValidCompany();
        company.WorkingDaysPerMonth = workingDays;

        Assert.False(CompanySetupRules.HasValidPayrollSettings(company));
    }

    [Fact]
    public void Payroll_settings_require_a_defined_daily_rate_method()
    {
        var company = ValidCompany();
        company.DailyRateMethod = (DailyRateMethod)999;

        Assert.False(CompanySetupRules.HasValidPayrollSettings(company));
    }

    [Fact]
    public void TryCanonicalizeWeeklyOffDays_normalizes_distinct_valid_days()
    {
        var valid = CompanySetupRules.TryCanonicalizeWeeklyOffDays(
            [" sunday ", "SATURDAY"],
            out var canonical);

        Assert.True(valid);
        Assert.Equal("Saturday,Sunday", canonical);
    }

    [Theory]
    [InlineData()]
    [InlineData("Funday")]
    [InlineData("Sunday", "sunday")]
    public void TryCanonicalizeWeeklyOffDays_rejects_missing_invalid_or_duplicate_days(params string[] days)
    {
        Assert.False(CompanySetupRules.TryCanonicalizeWeeklyOffDays(days, out _));
    }

    [Theory]
    [InlineData("Sunday,Saturday")]
    [InlineData("sunday")]
    [InlineData("Sunday,Sunday")]
    [InlineData("Funday")]
    [InlineData("")]
    public void Payroll_settings_require_canonical_weekly_off_storage(string weeklyOffDays)
    {
        var company = ValidCompany();
        company.WeeklyOffDays = weeklyOffDays;

        Assert.False(CompanySetupRules.HasValidPayrollSettings(company));
    }

    [Fact]
    public void Valid_company_details_and_payroll_settings_pass()
    {
        var company = ValidCompany();

        Assert.True(CompanySetupRules.HasValidCompanyDetails(company));
        Assert.True(CompanySetupRules.HasValidPayrollSettings(company));
    }

    [Fact]
    public void Completion_requires_valid_details_settings_and_logo()
    {
        var company = ValidCompany();
        company.LogoPath = null;
        Assert.False(CompanySetupRules.CanComplete(company));

        company.LogoPath = " uploads/companies/logo.png ";
        Assert.True(CompanySetupRules.CanComplete(company));

        company.WorkingDaysPerMonth = 0;
        Assert.False(CompanySetupRules.CanComplete(company));
    }

    [Fact]
    public void Completion_rejects_logo_paths_over_the_persisted_limit()
    {
        var company = ValidCompany();
        company.LogoPath = new string('l', CompanySetupRules.LogoPathMaxLength + 1);

        Assert.False(CompanySetupRules.CanComplete(company));
    }

    private static Company ValidCompany() => new()
    {
        Name = "ABC Traders",
        ContactEmail = "owner@example.com",
        ContactPhone = "1234567890",
        AddressLine1 = "Main Road",
        City = "Pune",
        State = "Maharashtra",
        PostalCode = "411001",
        LogoPath = "uploads/companies/logo.png",
        WorkingDaysPerMonth = 26,
        WeeklyOffDays = "Saturday,Sunday",
        DailyRateMethod = DailyRateMethod.CalendarDays
    };
}
