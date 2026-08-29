using MiniPayroll.Api.Auth;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class SalaryStructureEndpoints
{
    public static IEndpointRouteBuilder MapSalaryStructureEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/employees")
            .RequireAuthorization(CompanySetupAuthorization.Configure);

        group.MapGet("/{id:guid}/salary-structures", List);
        group.MapGet("/{id:guid}/salary-structure", GetEffective);
        group.MapPost("/{id:guid}/salary-structures", Create);
        return routes;
    }

    private static async Task<IResult> List(Guid id, SalaryStructureService salaries,
        CancellationToken cancellationToken) =>
        ToHttp(await salaries.ListAsync(id, cancellationToken));

    private static async Task<IResult> GetEffective(Guid id, DateOnly? effectiveOn,
        SalaryStructureService salaries, CancellationToken cancellationToken) =>
        ToHttp(await salaries.GetEffectiveAsync(id, effectiveOn, cancellationToken));

    private static async Task<IResult> Create(Guid id, SalaryStructureInput? input,
        SalaryStructureService salaries, CancellationToken cancellationToken) =>
        ToHttp(await salaries.CreateAsync(id, input, cancellationToken));

    private static IResult ToHttp(SalaryStructureResult result) =>
        result.Status == SalaryStructureStatusCode.Success
            ? result.Structures is not null
                ? Results.Ok(result.Structures)
                : Results.Ok(result.Structure)
            : Results.Json(new { error = ErrorMessage(result.Status) },
                statusCode: SalaryStructureHttpStatus.For(result.Status));

    private static string ErrorMessage(SalaryStructureStatusCode status) => status switch
    {
        SalaryStructureStatusCode.InvalidInput => "The salary structure is invalid. Include one fixed Basic Salary and valid component values.",
        SalaryStructureStatusCode.DuplicateEffectiveDate => "A salary structure already exists for this effective date.",
        SalaryStructureStatusCode.EmployeeNotFound => "The employee was not found.",
        SalaryStructureStatusCode.NotFound => "No salary structure is effective on that date.",
        SalaryStructureStatusCode.SetupIncomplete => "Complete company setup before managing salary structures.",
        SalaryStructureStatusCode.SubscriptionReadOnly => "This company can view salary structures but cannot change them right now.",
        SalaryStructureStatusCode.CompanyNotFound => "The tenant company was not found.",
        _ => "The salary structure request could not be completed."
    };
}

public static class SalaryStructureHttpStatus
{
    public static int For(SalaryStructureStatusCode status) => status switch
    {
        SalaryStructureStatusCode.Success => StatusCodes.Status200OK,
        SalaryStructureStatusCode.InvalidInput => StatusCodes.Status400BadRequest,
        SalaryStructureStatusCode.EmployeeNotFound or SalaryStructureStatusCode.NotFound => StatusCodes.Status404NotFound,
        SalaryStructureStatusCode.SetupIncomplete or SalaryStructureStatusCode.SubscriptionReadOnly
            or SalaryStructureStatusCode.DuplicateEffectiveDate => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}
