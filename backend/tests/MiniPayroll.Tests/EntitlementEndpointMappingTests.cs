using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Auth;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class EntitlementEndpointMappingTests
{
    [Fact]
    public void Registration_maps_entitlements_for_company_admins()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<IEntitlementService, EntitlementService>();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        var app = builder.Build();
        app.MapEntitlementEndpoints();

        var endpoint = Assert.Single(
            ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>());

        Assert.Equal("/api/entitlements", endpoint.RoutePattern.RawText);
        Assert.Contains(
            HttpMethods.Get,
            endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods);
        Assert.Contains(
            endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>().Requirements
                .OfType<RolesAuthorizationRequirement>(),
            requirement => requirement.AllowedRoles.Contains(RoleNames.CompanyAdmin));
        Assert.Single(
            endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>().Requirements
                .OfType<PasswordChangeCompletedRequirement>());
    }
}
