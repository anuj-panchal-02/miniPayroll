using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class BillingEndpointMappingTests
{
    [Fact]
    public void Billing_routes_are_mapped_on_companies()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        builder.Services.AddDbContext<MiniPayrollDbContext>(options =>
            options.UseInMemoryDatabase($"billing-endpoints-{Guid.NewGuid():N}"));
        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<MiniPayrollDbContext>();
        builder.Services.AddScoped<CompanyAdminService>();
        builder.Services.AddScoped<PayrollCalculationService>();
        builder.Services.AddScoped<BillingService>();
        var app = builder.Build();
        app.MapCompanyEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var patterns = endpoints.Select(endpoint => endpoint.RoutePattern.RawText).ToHashSet();

        Assert.Contains("/api/companies/{id:guid}/billing", patterns);
        Assert.Contains("/api/companies/{id:guid}/payments", patterns);
        Assert.All(
            endpoints,
            endpoint => Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public void Plan_and_company_billing_routes_are_mapped()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        builder.Services.AddScoped<BillingService>();
        builder.Services.AddScoped<PlanSettingsService>();
        var app = builder.Build();
        app.MapPlanEndpoints();
        app.MapCompanyBillingEndpoints();

        var patterns = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToHashSet();

        Assert.Contains("/api/company/billing", patterns);
        Assert.True(patterns.Any(pattern => pattern.StartsWith("/api/platform/plan", StringComparison.Ordinal)));
    }

    [Fact]
    public void Billing_statuses_map_to_http()
    {
        Assert.Equal(StatusCodes.Status400BadRequest, BillingHttpStatus.For(BillingStatusCode.InvalidInput));
        Assert.Equal(StatusCodes.Status400BadRequest, BillingHttpStatus.For(BillingStatusCode.InvalidPeriod));
        Assert.Equal(StatusCodes.Status404NotFound, BillingHttpStatus.For(BillingStatusCode.CompanyNotFound));
        Assert.Equal(StatusCodes.Status409Conflict, BillingHttpStatus.For(BillingStatusCode.NotActivated));
        Assert.Equal(StatusCodes.Status403Forbidden, BillingHttpStatus.For(BillingStatusCode.Forbidden));
    }
}
