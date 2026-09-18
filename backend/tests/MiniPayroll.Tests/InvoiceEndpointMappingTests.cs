using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Auth;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class InvoiceEndpointMappingTests
{
    [Fact]
    public void Superadmin_invoice_routes_are_named_commands()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        builder.Services.AddDbContext<MiniPayrollDbContext>(options =>
            options.UseInMemoryDatabase($"invoice-endpoints-{Guid.NewGuid():N}"));
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

        Assert.Contains(("/api/companies/{id:guid}/invoices", HttpMethods.Get), mapped);
        Assert.Contains(("/api/companies/{id:guid}/invoices", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/invoices/{invoiceId:guid}/issue", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/invoices/{invoiceId:guid}/payment-pending", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/invoices/{invoiceId:guid}/payments", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/invoices/{invoiceId:guid}/fail", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/invoices/{invoiceId:guid}/void", HttpMethods.Post), mapped);
        Assert.Contains(("/api/companies/{id:guid}/invoices/{invoiceId:guid}/refund", HttpMethods.Post), mapped);
        Assert.DoesNotContain(
            mapped,
            item => item.Route is not null
                && item.Route.Contains("invoice", StringComparison.OrdinalIgnoreCase)
                && item.Method == HttpMethods.Patch);
    }

    [Fact]
    public void Company_admin_invoice_routes_are_read_only()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<InvoiceService>();
        builder.Services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        var app = builder.Build();
        app.MapCompanyInvoiceEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == "/api/company/invoices/");
        Assert.Contains(
            endpoints,
            endpoint => endpoint.RoutePattern.RawText == "/api/company/invoices/{invoiceId:guid}");
        Assert.All(
            endpoints,
            endpoint =>
            {
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
            });
    }

    [Theory]
    [InlineData(InvoiceCommandStatus.InvalidInput, 400)]
    [InlineData(InvoiceCommandStatus.DuplicatePeriod, 409)]
    [InlineData(InvoiceCommandStatus.NotFound, 404)]
    [InlineData(InvoiceCommandStatus.Forbidden, 403)]
    [InlineData(InvoiceCommandStatus.Overpay, 400)]
    public void Statuses_map_to_http(InvoiceCommandStatus status, int expected)
    {
        Assert.Equal(expected, InvoiceHttpStatus.For(status));
    }
}
