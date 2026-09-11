using Microsoft.Extensions.Options;
using MiniPayroll.Api.Auth;
using MiniPayroll.Api.Storage;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class PayrollEndpoints
{
    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payroll")
            .RequireAuthorization(CompanySetupAuthorization.Configure);

        group.MapGet("/runs", ListRuns);
        group.MapGet("/{year:int}/{month:int}", GetPeriod);
        group.MapPost("/{year:int}/{month:int}/run", CreateRun);
        group.MapPost("/{year:int}/{month:int}/calculate", Calculate);
        group.MapPut("/runs/{runId:guid}/inputs", SaveInputs);
        group.MapPost("/runs/{runId:guid}/finalize", FinalizeRun);
        group.MapPut("/runs/{runId:guid}/employees/{employeeId:guid}/payment", SetPayment);
        group.MapPut("/runs/{runId:guid}/employees/{employeeId:guid}/statutory-overrides", SetStatutoryOverrides);
        group.MapGet("/runs/{runId:guid}/payslips", CombinedPayslips);
        group.MapGet("/runs/{runId:guid}/payslips/{employeeId:guid}", EmployeePayslip);
        return routes;
    }

    private static async Task<IResult> GetPeriod(int year, int month,
        PayrollInputService inputs, CancellationToken cancellationToken) =>
        ToHttp(await inputs.GetPeriodAsync(year, month, cancellationToken));

    private static async Task<IResult> ListRuns(
        PayrollInputService inputs, CancellationToken cancellationToken) =>
        ToHttp(await inputs.ListRunsAsync(cancellationToken));

    private static async Task<IResult> CreateRun(int year, int month,
        PayrollCalculationService payroll, CancellationToken cancellationToken) =>
        ToHttp(await payroll.CreateRunAsync(year, month, cancellationToken));

    private static async Task<IResult> Calculate(int year, int month,
        PayrollCalculationService payroll, CancellationToken cancellationToken) =>
        ToHttp(await payroll.CalculatePeriodAsync(year, month, cancellationToken));

    private static async Task<IResult> SaveInputs(Guid runId, PayrollInputsPayload? input,
        PayrollInputService inputs, CancellationToken cancellationToken) =>
        ToHttp(await inputs.SaveInputsAsync(runId, input, cancellationToken));

    private static async Task<IResult> FinalizeRun(Guid runId,
        PayrollCalculationService payroll, CancellationToken cancellationToken) =>
        ToHttp(await payroll.FinalizeAsync(runId, cancellationToken));

    private static async Task<IResult> SetPayment(
        Guid runId,
        Guid employeeId,
        PayrollPaymentPayload? input,
        PayrollInputService inputs,
        CancellationToken cancellationToken) =>
        ToHttp(await inputs.SetPaymentAsync(runId, employeeId, input, cancellationToken));

    private static async Task<IResult> SetStatutoryOverrides(
        Guid runId,
        Guid employeeId,
        StatutoryOverridesPayload? input,
        PayrollCalculationService payroll,
        CancellationToken cancellationToken) =>
        ToHttp(await payroll.SetStatutoryOverridesAsync(runId, employeeId, input?.Overrides, cancellationToken));

    private static async Task<IResult> CombinedPayslips(
        Guid runId,
        PayrollPayslipService payslips,
        IOptions<CompanyLogoStorageOptions> storage,
        CancellationToken cancellationToken) =>
        ToHttp(await payslips.GetCombinedAsync(
            runId, path => CompanyLogoBytes.TryRead(path, storage.Value), cancellationToken));

    private static async Task<IResult> EmployeePayslip(
        Guid runId,
        Guid employeeId,
        PayrollPayslipService payslips,
        IOptions<CompanyLogoStorageOptions> storage,
        CancellationToken cancellationToken) =>
        ToHttp(await payslips.GetEmployeeAsync(
            runId, employeeId, path => CompanyLogoBytes.TryRead(path, storage.Value), cancellationToken));

    private static IResult ToHttp(PayrollRunResult result) =>
        result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Run)
            : Error(result.Status);

    private static IResult ToHttp(PayrollPeriodResult result) =>
        result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Period)
            : Error(result.Status);

    private static IResult ToHttp(PayrollHistoryResult result) =>
        result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Runs)
            : Error(result.Status);

    private static IResult ToHttp(PayslipFileResult result) =>
        result.Status == PayrollRunStatusCode.Success && result.File is not null
            ? Results.File(result.File.Content, "application/pdf", result.File.FileName)
            : Error(result.Status);

    private static IResult Error(PayrollRunStatusCode status) =>
        Results.Json(new { error = ErrorMessage(status) },
            statusCode: PayrollHttpStatus.For(status));

    private static string ErrorMessage(PayrollRunStatusCode status) => status switch
    {
        PayrollRunStatusCode.InvalidPeriod => "The payroll period is invalid.",
        PayrollRunStatusCode.InvalidInput => "The payroll inputs are invalid.",
        PayrollRunStatusCode.DuplicateRun => "A payroll run already exists for this period.",
        PayrollRunStatusCode.NotFound => "The payroll run was not found.",
        PayrollRunStatusCode.NotCalculated => "This payroll run is not ready for that action.",
        PayrollRunStatusCode.Forbidden => "You are not allowed to perform this action.",
        PayrollRunStatusCode.RunLocked => "This payroll run is finalized or reversed and cannot be changed.",
        PayrollRunStatusCode.ConcurrencyConflict => "This payroll run was changed by someone else. Reload and try again.",
        PayrollRunStatusCode.SetupIncomplete => "Complete company setup before running payroll.",
        PayrollRunStatusCode.SubscriptionReadOnly => "This company cannot run payroll right now.",
        PayrollRunStatusCode.CompanyNotFound => "The tenant company was not found.",
        _ => "The payroll request could not be completed."
    };
}

public static class PayrollHttpStatus
{
    public static int For(PayrollRunStatusCode status) => status switch
    {
        PayrollRunStatusCode.Success => StatusCodes.Status200OK,
        PayrollRunStatusCode.InvalidPeriod or PayrollRunStatusCode.InvalidInput => StatusCodes.Status400BadRequest,
        PayrollRunStatusCode.NotFound => StatusCodes.Status404NotFound,
        PayrollRunStatusCode.DuplicateRun or PayrollRunStatusCode.RunLocked
            or PayrollRunStatusCode.NotCalculated
            or PayrollRunStatusCode.ConcurrencyConflict
            or PayrollRunStatusCode.SetupIncomplete
            or PayrollRunStatusCode.SubscriptionReadOnly => StatusCodes.Status409Conflict,
        PayrollRunStatusCode.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };
}
