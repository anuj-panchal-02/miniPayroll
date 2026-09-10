using System.Security.Claims;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class LocationCatalogEndpoints
{
    public static IEndpointRouteBuilder MapLocationCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        var read = routes.MapGroup("/api/platform").RequireAuthorization();
        read.MapGet("/states", ListStates);
        read.MapGet("/cities", ListCities);

        var write = routes.MapGroup("/api/platform").RequireAuthorization(policy =>
            policy.RequireRole(RoleNames.Superadmin));
        write.MapPost("/states", CreateState);
        write.MapPatch("/states/{id:guid}", UpdateState);
        write.MapPost("/cities", CreateCity);
        write.MapPatch("/cities/{id:guid}", UpdateCity);

        return routes;
    }

    private static async Task<IResult> ListStates(
        ClaimsPrincipal user,
        LocationCatalogService catalog,
        CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        var states = await catalog.ListStatesAsync(
            AllowInactive(user, includeInactive),
            cancellationToken);
        return Results.Ok(states);
    }

    private static async Task<IResult> ListCities(
        ClaimsPrincipal user,
        LocationCatalogService catalog,
        CancellationToken cancellationToken,
        Guid? stateId = null,
        bool includeInactive = false)
    {
        if (stateId is not { } id)
        {
            return Results.BadRequest(new { error = "stateId is required." });
        }

        var result = await catalog.ListCitiesAsync(
            id,
            AllowInactive(user, includeInactive),
            cancellationToken);
        return ToHttp(result);
    }

    private static async Task<IResult> CreateState(
        LocationStateWriteRequest? request,
        LocationCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var result = await catalog.CreateStateAsync(request?.Name, request?.Code, cancellationToken);
        return ToHttp(result, created: true);
    }

    private static async Task<IResult> UpdateState(
        Guid id,
        LocationStateWriteRequest? request,
        LocationCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var result = await catalog.UpdateStateAsync(
            id,
            request?.Name,
            request?.Code,
            request?.IsActive,
            cancellationToken);
        return ToHttp(result);
    }

    private static async Task<IResult> CreateCity(
        LocationCityWriteRequest? request,
        LocationCatalogService catalog,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "A city payload is required." });
        }

        var result = await catalog.CreateCityAsync(request.StateId, request.Name, cancellationToken);
        return ToHttp(result, created: true);
    }

    private static async Task<IResult> UpdateCity(
        Guid id,
        LocationCityPatchRequest? request,
        LocationCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var result = await catalog.UpdateCityAsync(
            id,
            request?.Name,
            request?.IsActive,
            cancellationToken);
        return ToHttp(result);
    }

    private static bool AllowInactive(ClaimsPrincipal user, bool includeInactive) =>
        includeInactive && user.IsInRole(RoleNames.Superadmin);

    private static IResult ToHttp<T>(LocationCatalogResult<T> result, bool created = false) =>
        result.Status switch
        {
            LocationCatalogStatus.Success when created => Results.Created((string?)null, result.Value),
            LocationCatalogStatus.Success => Results.Ok(result.Value),
            LocationCatalogStatus.InvalidInput => Results.BadRequest(new { error = "The location details are invalid." }),
            LocationCatalogStatus.Duplicate => Results.Conflict(new { error = "A location with that name already exists." }),
            LocationCatalogStatus.NotFound => Results.NotFound(new { error = "The location was not found." }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };

    public sealed record LocationStateWriteRequest(string? Name, string? Code, bool? IsActive);
    public sealed record LocationCityWriteRequest(Guid StateId, string? Name);
    public sealed record LocationCityPatchRequest(string? Name, bool? IsActive);
}
