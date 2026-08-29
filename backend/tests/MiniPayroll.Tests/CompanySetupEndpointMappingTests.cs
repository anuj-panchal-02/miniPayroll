using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniPayroll.Api.Auth;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Api.Storage;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class CompanySetupEndpointMappingTests
{
    [Fact]
    public void Registration_maps_exact_plan_routes_with_company_admin_authorization()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<CompanySetupService>();
        builder.Services.AddScoped<CompanyLogoUploadCoordinator>();
        builder.Services.AddSingleton<ICompanyLogoStorage, UnusedLogoStorage>();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        builder.Services.AddSingleton(Options.Create(new CompanyLogoStorageOptions()));
        var app = builder.Build();
        app.MapCompanySetupEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var expected = new Dictionary<string, string>
        {
            ["/api/company/setup"] = HttpMethods.Get,
            ["/api/company/setup/details"] = HttpMethods.Patch,
            ["/api/company/setup/payroll-settings"] = HttpMethods.Patch,
            ["/api/company/setup/logo"] = HttpMethods.Post,
            ["/api/company/setup/complete"] = HttpMethods.Post
        };

        Assert.Equal(expected.Count, endpoints.Length);
        foreach (var endpoint in endpoints)
        {
            var rawRoute = endpoint.RoutePattern.RawText;
            var route = rawRoute == "/api/company/setup/"
                ? rawRoute.TrimEnd('/')
                : rawRoute;
            Assert.False(string.IsNullOrEmpty(route));
            Assert.True(expected.TryGetValue(route, out var expectedMethod), route);
            Assert.Contains(
                expectedMethod,
                endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods);
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            var policy = endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>();
            Assert.Contains(
                policy.Requirements.OfType<RolesAuthorizationRequirement>(),
                requirement => requirement.AllowedRoles.Contains(RoleNames.CompanyAdmin));
            Assert.Single(policy.Requirements.OfType<PasswordChangeCompletedRequirement>());
        }
    }

    [Fact]
    public void The_password_change_gate_is_scoped_to_company_setup()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        builder.Services.AddDbContext<MiniPayrollDbContext>(options =>
            options.UseInMemoryDatabase($"endpoint-metadata-{Guid.NewGuid():N}"));
        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<MiniPayrollDbContext>();
        builder.Services.AddScoped<CompanyAdminService>();
        var app = builder.Build();
        app.MapCompanyEndpoints();

        var policies = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Select(endpoint => endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>())
            .ToArray();

        Assert.NotEmpty(policies);
        Assert.All(
            policies,
            policy => Assert.Empty(policy.Requirements.OfType<PasswordChangeCompletedRequirement>()));
    }

    [Theory]
    [InlineData(CompanySetupStatus.Success, 200)]
    [InlineData(CompanySetupStatus.InvalidInput, 400)]
    [InlineData(CompanySetupStatus.CompanyNotFound, 404)]
    [InlineData(CompanySetupStatus.InvalidStep, 409)]
    [InlineData(CompanySetupStatus.AlreadyComplete, 409)]
    [InlineData(CompanySetupStatus.Conflict, 409)]
    public void Statuses_map_consistently(
        CompanySetupStatus status,
        int expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, CompanySetupHttpStatus.For(status));
    }

    [Fact]
    public void Multipart_limit_allows_only_small_protocol_overhead()
    {
        const long maxLogoBytes = 2 * 1024 * 1024;

        var bodyLimit = CompanyLogoUploadLimits.MultipartBodyLengthLimit(maxLogoBytes);

        Assert.InRange(bodyLimit, maxLogoBytes + 1, maxLogoBytes + (64 * 1024));
        Assert.False(CompanyLogoUploadLimits.IsMultipartBodyTooLarge(bodyLimit, maxLogoBytes));
        Assert.True(CompanyLogoUploadLimits.IsMultipartBodyTooLarge(bodyLimit + 1, maxLogoBytes));
    }

    [Fact]
    public void File_limit_does_not_include_multipart_overhead()
    {
        const long maxLogoBytes = 2 * 1024 * 1024;

        Assert.False(CompanyLogoUploadLimits.IsFileTooLarge(maxLogoBytes, maxLogoBytes));
        Assert.True(CompanyLogoUploadLimits.IsFileTooLarge(maxLogoBytes + 1, maxLogoBytes));
    }

    private sealed class UnusedLogoStorage : ICompanyLogoStorage
    {
        public Task<StoredCompanyLogo> SaveAsync(
            Guid tenantCompanyId,
            Stream content,
            string? originalFileName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
