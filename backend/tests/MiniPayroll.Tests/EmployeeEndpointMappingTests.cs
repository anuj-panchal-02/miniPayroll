using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Auth;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class EmployeeEndpointMappingTests
{
    [Fact]
    public void Registration_maps_employee_routes_with_company_admin_authorization()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<EmployeeService>();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        var app = builder.Build();
        app.MapEmployeeEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var expected = new Dictionary<string, string>
        {
            ["/api/employees"] = HttpMethods.Get,
            ["/api/employees/{id:guid}"] = HttpMethods.Get,
            ["/api/employees/"] = HttpMethods.Post
        };

        var byRoute = endpoints
            .Select(endpoint => (
                Route: Normalize(endpoint.RoutePattern.RawText),
                Methods: endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods,
                Endpoint: endpoint))
            .ToList();

        Assert.Contains(byRoute, item => item.Route is "/api/employees" or "/api/employees/"
            && item.Methods.Contains(HttpMethods.Get));
        Assert.Contains(byRoute, item => item.Route is "/api/employees" or "/api/employees/"
            && item.Methods.Contains(HttpMethods.Post));
        Assert.Contains(byRoute, item => item.Route.Contains("{id:guid}", StringComparison.Ordinal)
            && item.Methods.Contains(HttpMethods.Get));
        Assert.Contains(byRoute, item => item.Route.Contains("{id:guid}", StringComparison.Ordinal)
            && item.Methods.Contains(HttpMethods.Patch));

        Assert.All(endpoints, endpoint =>
        {
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            var policy = endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>();
            Assert.Contains(
                policy.Requirements.OfType<RolesAuthorizationRequirement>(),
                requirement => requirement.AllowedRoles.Contains(RoleNames.CompanyAdmin));
            Assert.Single(policy.Requirements.OfType<PasswordChangeCompletedRequirement>());
        });
    }

    [Theory]
    [InlineData(EmployeeStatusCode.InvalidInput, StatusCodes.Status400BadRequest)]
    [InlineData(EmployeeStatusCode.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(EmployeeStatusCode.SetupIncomplete, StatusCodes.Status409Conflict)]
    [InlineData(EmployeeStatusCode.EmployeeLimitReached, StatusCodes.Status409Conflict)]
    [InlineData(EmployeeStatusCode.DuplicateEmployeeCode, StatusCodes.Status409Conflict)]
    [InlineData(EmployeeStatusCode.SubscriptionReadOnly, StatusCodes.Status409Conflict)]
    public void Status_codes_match_the_plan(EmployeeStatusCode status, int expected)
    {
        Assert.Equal(expected, EmployeeHttpStatus.For(status));
    }

    private static string Normalize(string? route) =>
        string.IsNullOrEmpty(route) ? string.Empty : route.TrimEnd('/');
}
