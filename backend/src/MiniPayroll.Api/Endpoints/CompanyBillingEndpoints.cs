using MiniPayroll.Api.Auth;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class CompanyBillingEndpoints
{
    public static IEndpointRouteBuilder MapCompanyBillingEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/company/billing", Get)
            .RequireAuthorization(CompanySetupAuthorization.Configure);
        return routes;
    }

    private static async Task<IResult> Get(
        BillingService billing,
        CancellationToken cancellationToken)
    {
        var result = await billing.GetOwnAsync(cancellationToken);
        return result.Status == BillingStatusCode.Success
            ? Results.Ok(result.Billing)
            : Results.Json(
                new { error = result.Status switch
                {
                    BillingStatusCode.Forbidden => "You are not allowed to view billing.",
                    BillingStatusCode.CompanyNotFound => "The company was not found.",
                    _ => "The billing request could not be completed."
                }},
                statusCode: BillingHttpStatus.For(result.Status));
    }
}
