using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll;

public sealed record PayrollSourceSnapshot(
    DailyRateMethod DailyRateMethod,
    DateOnly AsOf,
    DateOnly PfRuleEffectiveFrom,
    DateOnly EsiRuleEffectiveFrom,
    decimal PfEmployeeRate,
    decimal PfEmployerRate,
    decimal PfWageCeiling,
    decimal EsiEmployeeRate,
    decimal EsiEmployerRate,
    decimal EsiEligibilityCeiling,
    PayrollCompanyStatutorySource Company,
    IReadOnlyList<PayrollEmployeeSource> Employees);

public sealed record PayrollCompanyStatutorySource(
    bool PfApplicable,
    bool PfUseWageCeiling,
    bool EsiApplicable,
    string? State);

public sealed record PayrollEmployeeSource(
    Guid EmployeeId,
    string EmployeeCode,
    string FullName,
    string Designation,
    DateOnly? JoiningDate,
    DateOnly? ExitDate,
    EmployeeStatus Status,
    Gender? Gender,
    bool PfCovered,
    bool EsiCovered,
    decimal? OvertimeRate,
    DateOnly? StructureEffectiveFrom,
    IReadOnlyList<PayrollStructureComponentSource> Structure,
    PayrollAttendanceSource? Attendance,
    IReadOnlyList<PayrollOvertimeSource> Overtime,
    IReadOnlyList<PayrollBonusSource> Bonuses,
    IReadOnlyList<PayrollDeductionSource> Deductions,
    IReadOnlyList<PayrollOverrideSource> Overrides);

public sealed record PayrollStructureComponentSource(
    string Name,
    SalaryComponentType Type,
    SalaryComponentValueType ValueType,
    decimal Value,
    int SortOrder,
    MiniPayroll.Domain.Payroll.Statutory.SalaryComponentKind Kind);

public sealed record PayrollAttendanceSource(
    decimal WorkingDays,
    decimal Present,
    decimal PaidLeave,
    decimal UnpaidLeave);

public sealed record PayrollOvertimeSource(decimal Hours, decimal? Rate, decimal? AppliedRate);

public sealed record PayrollBonusSource(BonusType Type, decimal Amount);

public sealed record PayrollDeductionSource(OneTimeDeductionType Type, decimal Amount);

public sealed record PayrollOverrideSource(MiniPayroll.Domain.Payroll.Statutory.StatutoryKind Kind, decimal Amount);

public static class PayrollSourceFingerprint
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public static string CanonicalJson(PayrollSourceSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);

    public static string Hash(PayrollSourceSnapshot snapshot)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson(snapshot)));
        return Convert.ToHexString(bytes);
    }

    public static bool HasDrift(string? storedFingerprint, PayrollSourceSnapshot live) =>
        string.IsNullOrEmpty(storedFingerprint) || storedFingerprint != Hash(live);

    public static void Apply(PayrollRun run, PayrollSourceSnapshot snapshot)
    {
        run.SourceSnapshotJson = CanonicalJson(snapshot);
        run.SourceFingerprint = Hash(snapshot);
        run.PfRuleEffectiveFrom = snapshot.PfRuleEffectiveFrom;
        run.EsiRuleEffectiveFrom = snapshot.EsiRuleEffectiveFrom;
    }

    public static void Clear(PayrollRun run)
    {
        run.SourceSnapshotJson = null;
        run.SourceFingerprint = null;
        run.PfRuleEffectiveFrom = null;
        run.EsiRuleEffectiveFrom = null;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new CanonicalDecimalConverter());
        options.Converters.Add(new CanonicalNullableDecimalConverter());
        return options;
    }

    private sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("0.################", CultureInfo.InvariantCulture));
    }

    private sealed class CanonicalNullableDecimalConverter : JsonConverter<decimal?>
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            return decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToString("0.################", CultureInfo.InvariantCulture));
        }
    }
}
