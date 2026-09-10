using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class LocationCatalogEndpointMappingTests
{
    [Fact]
    public void Read_routes_require_authentication_and_writes_require_superadmin()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<LocationCatalogService>();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        var app = builder.Build();
        app.MapLocationCatalogEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        Assert.Equal(6, endpoints.Length);
        Assert.All(
            endpoints,
            endpoint => Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));

        var writes = endpoints.Where(endpoint =>
        {
            var methods = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods;
            return methods.Contains(HttpMethods.Post) || methods.Contains(HttpMethods.Patch);
        });

        Assert.Equal(4, writes.Count());
        Assert.All(writes, endpoint =>
        {
            var policy = endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>();
            Assert.Contains(
                policy.Requirements.OfType<RolesAuthorizationRequirement>(),
                requirement => requirement.AllowedRoles.Contains(RoleNames.Superadmin));
        });
    }

    [Fact]
    public void List_query_flags_default_to_active_only_when_omitted()
    {
        var flags = typeof(LocationCatalogEndpoints).GetMethods(
                BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.Name is "ListStates" or "ListCities")
            .Select(method => method.GetParameters().Single(parameter => parameter.Name == "includeInactive"))
            .ToArray();

        Assert.Equal(2, flags.Length);
        Assert.All(flags, parameter =>
        {
            Assert.True(parameter.HasDefaultValue);
            Assert.Equal(false, parameter.DefaultValue);
        });
    }
}
