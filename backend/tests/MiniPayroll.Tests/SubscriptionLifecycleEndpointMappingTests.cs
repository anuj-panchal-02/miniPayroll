using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class SubscriptionLifecycleEndpointMappingTests
{
    [Fact]
    public void Registration_maps_named_subscription_actions_for_superadmin()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        builder.Services.AddDbContext<MiniPayrollDbContext>(options =>
            options.UseInMemoryDatabase($"lifecycle-endpoints-{Guid.NewGuid():N}"));
        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<MiniPayrollDbContext>();
        builder.Services.AddScoped<CompanyAdminService>();
        builder.Services.AddScoped<SubscriptionLifecycleService>();
        builder.Services.AddScoped<InvoiceService>();
        builder.Services.AddScoped<PayrollCalculationService>();
        builder.Services.AddScoped<BillingService>();
        var app = builder.Build();
        app.MapCompanyEndpoints();

        var mapped = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => (
                Route: endpoint.RoutePattern.RawText,
                Method: endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods[0]))
            .ToHashSet();

        Assert.Contains(("/api/companies/{id:guid}/activate", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/subscription/past-due", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/subscription/grace", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/subscription/suspend", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/subscription/cancel", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/subscription/expire", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/subscription/reactivate", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/subscription/plan", HttpMethods.Post), mapped);
        Assert.DoesNotContain(
            mapped,
            item => item.Route?.Contains("status", StringComparison.OrdinalIgnoreCase) == true);

        Assert.All(
            ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .Select(endpoint => endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>()),
            policy => Assert.Contains(
                policy.Requirements.OfType<RolesAuthorizationRequirement>(),
                requirement => requirement.AllowedRoles.Contains(RoleNames.Superadmin)));
    }
}
