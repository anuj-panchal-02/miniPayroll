using MiniPayroll.Api.Auth;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class PayrollEndpoints
{
    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payroll")
            .RequireAuthorization(CompanySetupAuthorization.Configure);

        group.MapGet("/{year:int}/{month:int}", GetPeriod);
        group.MapPost("/{year:int}/{month:int}/run", CreateRun);
        group.MapPost("/{year:int}/{month:int}/calculate", Calculate);
        group.MapPut("/runs/{runId:guid}/inputs", SaveInputs);
        return routes;
    }

    private static async Task<IResult> GetPeriod(int year, int month,
        PayrollInputService inputs, CancellationToken cancellationToken) =>
        ToHttp(await inputs.GetPeriodAsync(year, month, cancellationToken));

    private static async Task<IResult> CreateRun(int year, int month,
        PayrollCalculationService payroll, CancellationToken cancellationToken) =>
        ToHttp(await payroll.CreateRunAsync(year, month, cancellationToken));

    private static async Task<IResult> Calculate(int year, int month,
        PayrollCalculationService payroll, CancellationToken cancellationToken) =>
        ToHttp(await payroll.CalculatePeriodAsync(year, month, cancellationToken));

    private static async Task<IResult> SaveInputs(Guid runId, PayrollInputsPayload? input,
        PayrollInputService inputs, CancellationToken cancellationToken) =>
        ToHttp(await inputs.SaveInputsAsync(runId, input, cancellationToken));

    private static IResult ToHttp(PayrollRunResult result) =>
        result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Run)
            : Error(result.Status);

    private static IResult ToHttp(PayrollPeriodResult result) =>
        result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Period)
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
            or PayrollRunStatusCode.ConcurrencyConflict
            or PayrollRunStatusCode.SetupIncomplete
            or PayrollRunStatusCode.SubscriptionReadOnly => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}
