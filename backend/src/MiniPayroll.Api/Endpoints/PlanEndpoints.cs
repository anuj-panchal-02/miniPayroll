using MiniPayroll.Domain.Constants;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class PlanEndpoints
{
    public static IEndpointRouteBuilder MapPlanEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/platform/plan")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Superadmin));
        group.MapGet(string.Empty, Get);
        group.MapPatch(string.Empty, Update);
        return routes;
    }

    private static async Task<IResult> Get(
        PlanSettingsService plans,
        CancellationToken cancellationToken) =>
        ToHttp(await plans.GetAsync(cancellationToken));

    private static async Task<IResult> Update(
        UpdatePlanRequest? request,
        PlanSettingsService plans,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ToHttp(new PlanSettingsResult(PlanSettingsStatus.InvalidInput));
        }

        return ToHttp(await plans.UpdateAsync(request.PricePerEmployee, cancellationToken));
    }

    private static IResult ToHttp(PlanSettingsResult result) =>
        result.Status == PlanSettingsStatus.Success
            ? Results.Ok(result.Plan)
            : Results.Json(
                new { error = result.Status switch
                {
                    PlanSettingsStatus.InvalidInput => "Enter a price greater than zero, with at most two decimals.",
                    PlanSettingsStatus.NotFound => "The Basic plan was not found.",
                    PlanSettingsStatus.Forbidden => "You are not allowed to change the plan.",
                    _ => "The plan request could not be completed."
                }},
                statusCode: result.Status switch
                {
                    PlanSettingsStatus.InvalidInput => StatusCodes.Status400BadRequest,
                    PlanSettingsStatus.NotFound => StatusCodes.Status404NotFound,
                    PlanSettingsStatus.Forbidden => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status500InternalServerError
                });

    public sealed record UpdatePlanRequest(decimal PricePerEmployee);
}
