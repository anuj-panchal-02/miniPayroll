using MiniPayroll.Api.Auth;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Api.Endpoints;

public static class EntitlementEndpoints
{
    public static IEndpointRouteBuilder MapEntitlementEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/entitlements", Get)
            .RequireAuthorization(CompanySetupAuthorization.Configure);
        return routes;
    }

    private static async Task<IResult> Get(
        IEntitlementService entitlements,
        ITenantContext tenant,
        CancellationToken cancellationToken)
    {
        if (tenant.CompanyId is not { } companyId)
        {
            return Results.NotFound();
        }

        var snapshot = await entitlements.GetSnapshot(companyId, cancellationToken);
        return Results.Ok(ToResponse(snapshot));
    }

    public static EntitlementResponse ToResponse(EntitlementSnapshot snapshot) =>
        new(
            snapshot.Usage.CurrentUsage,
            snapshot.Usage.MaximumAllowed,
            snapshot.Usage.Remaining,
            snapshot.Usage.CanAdd,
            snapshot.CanRunPayroll,
            snapshot.EnabledFeatures);
}

public sealed record EntitlementResponse(
    int CurrentUsage,
    int MaximumAllowed,
    int Remaining,
    bool CanAdd,
    bool CanRunPayroll,
    IReadOnlyList<string> EnabledFeatures);
