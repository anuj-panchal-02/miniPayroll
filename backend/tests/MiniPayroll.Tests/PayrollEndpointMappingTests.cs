using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PayrollEndpointMappingTests
{
    [Fact]
    public void Payroll_routes_are_mapped_and_require_authorization()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<PayrollCalculationService>();
        builder.Services.AddScoped<PayrollInputService>();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        var app = builder.Build();
        app.MapPayrollEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        var patterns = endpoints.Select(endpoint => endpoint.RoutePattern.RawText).ToHashSet();
        Assert.Contains("/api/payroll/{year:int}/{month:int}", patterns);
        Assert.Contains("/api/payroll/{year:int}/{month:int}/run", patterns);
        Assert.Contains("/api/payroll/{year:int}/{month:int}/calculate", patterns);
        Assert.Contains("/api/payroll/runs/{runId:guid}/inputs", patterns);
        Assert.Equal(4, endpoints.Length);
        Assert.All(
            endpoints,
            endpoint => Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public void Invalid_input_maps_to_bad_request_and_locked_runs_to_conflict()
    {
        Assert.Equal(StatusCodes.Status400BadRequest,
            PayrollHttpStatus.For(PayrollRunStatusCode.InvalidInput));
        Assert.Equal(StatusCodes.Status400BadRequest,
            PayrollHttpStatus.For(PayrollRunStatusCode.InvalidPeriod));
        Assert.Equal(StatusCodes.Status409Conflict,
            PayrollHttpStatus.For(PayrollRunStatusCode.RunLocked));
        Assert.Equal(StatusCodes.Status409Conflict,
            PayrollHttpStatus.For(PayrollRunStatusCode.ConcurrencyConflict));
    }
}
