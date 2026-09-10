using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Api.Endpoints;

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/platform", Current);
        return routes;
    }

    public static IResult Current() => Results.Ok(ToResponse());

    public static PlatformLimitsResponse ToResponse() => new(
        PlatformLimits.MinEmployeeLimit,
        PlatformLimits.HardEmployeeCap,
        PlatformLimits.DefaultEmployeeLimit,
        PlatformLimits.DefaultPlanName,
        PlatformLimits.CurrencyCode);

    public sealed record PlatformLimitsResponse(
        int MinEmployeeLimit,
        int HardEmployeeCap,
        int DefaultEmployeeLimit,
        string DefaultPlanName,
        string CurrencyCode);
}
