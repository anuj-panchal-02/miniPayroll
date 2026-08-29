using MiniPayroll.Api.Auth;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/employees")
            .RequireAuthorization(CompanySetupAuthorization.Configure);

        group.MapGet(string.Empty, List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost(string.Empty, Create);
        group.MapPatch("/{id:guid}", Update);
        return routes;
    }

    private static async Task<IResult> List(
        EmployeeService employees,
        CancellationToken cancellationToken) =>
        ToHttp(await employees.ListAsync(cancellationToken));

    private static async Task<IResult> Get(
        Guid id,
        EmployeeService employees,
        CancellationToken cancellationToken) =>
        ToHttp(await employees.GetAsync(id, cancellationToken));

    private static async Task<IResult> Create(
        EmployeeInput? input,
        EmployeeService employees,
        CancellationToken cancellationToken) =>
        ToHttp(await employees.CreateAsync(input, cancellationToken));

    private static async Task<IResult> Update(
        Guid id,
        EmployeeInput? input,
        EmployeeService employees,
        CancellationToken cancellationToken) =>
        ToHttp(await employees.UpdateAsync(id, input, cancellationToken));

    private static IResult ToHttp(EmployeeResult result)
    {
        if (result.Status == EmployeeStatusCode.Success)
        {
            return Results.Ok(result.List is not null ? result.List : result.Employee);
        }

        return Results.Json(
            new EmployeeError(
                ErrorMessage(result.Status, result.EmployeeLimit),
                result.EmployeeLimit),
            statusCode: EmployeeHttpStatus.For(result.Status));
    }

    private static string ErrorMessage(EmployeeStatusCode status, int? employeeLimit) =>
        status switch
        {
            EmployeeStatusCode.InvalidInput => "The employee details are invalid.",
            EmployeeStatusCode.CompanyNotFound => "The tenant company was not found.",
            EmployeeStatusCode.NotFound => "The employee was not found.",
            EmployeeStatusCode.SetupIncomplete => "Complete company setup before managing employees.",
            EmployeeStatusCode.SubscriptionReadOnly =>
                "This company can view employees but cannot add or edit them right now.",
            EmployeeStatusCode.DuplicateEmployeeCode => "An employee with this ID already exists.",
            EmployeeStatusCode.EmployeeLimitReached =>
                string.Format(EmployeeService.LimitReachedMessage, employeeLimit ?? 0),
            _ => "The employee request could not be completed."
        };
}

public static class EmployeeHttpStatus
{
    public static int For(EmployeeStatusCode status) =>
        status switch
        {
            EmployeeStatusCode.Success => StatusCodes.Status200OK,
            EmployeeStatusCode.InvalidInput => StatusCodes.Status400BadRequest,
            EmployeeStatusCode.CompanyNotFound or EmployeeStatusCode.NotFound =>
                StatusCodes.Status404NotFound,
            EmployeeStatusCode.SetupIncomplete
                or EmployeeStatusCode.SubscriptionReadOnly
                or EmployeeStatusCode.DuplicateEmployeeCode
                or EmployeeStatusCode.EmployeeLimitReached =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
}

public sealed record EmployeeError(string Error, int? EmployeeLimit);
