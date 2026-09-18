using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Auth;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Infrastructure.Payments;

namespace MiniPayroll.Tests;

public sealed class PaymentEndpointMappingTests
{
    [Fact]
    public void Payment_routes_require_company_admin_or_superadmin()
    {
        var endpoints = Map();
        var payments = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/payments", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(payments, endpoint => endpoint.RoutePattern.RawText == "/api/payments/checkout");
        Assert.Contains(payments, endpoint => endpoint.RoutePattern.RawText == "/api/payments/verify");
        Assert.Contains(payments, endpoint => endpoint.RoutePattern.RawText == "/api/payments/recurring");
        Assert.Contains(payments, endpoint => endpoint.RoutePattern.RawText == "/api/payments/recurring/cancel");
        Assert.All(payments, endpoint =>
        {
            Assert.Contains(
                HttpMethods.Post,
                endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods);
            var roles = endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>().Requirements
                .OfType<RolesAuthorizationRequirement>()
                .SelectMany(requirement => requirement.AllowedRoles)
                .ToHashSet();
            Assert.Contains(RoleNames.CompanyAdmin, roles);
            Assert.Contains(RoleNames.Superadmin, roles);
            Assert.Single(
                endpoint.Metadata.GetRequiredMetadata<AuthorizationPolicy>().Requirements
                    .OfType<PasswordChangeCompletedRequirement>());
        });
    }

    [Fact]
    public void Razorpay_webhook_is_anonymous()
    {
        var webhook = Assert.Single(
            Map(),
            endpoint => endpoint.RoutePattern.RawText == "/api/webhooks/razorpay");
        Assert.Contains(
            HttpMethods.Post,
            webhook.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods);
        Assert.Contains(webhook.Metadata, metadata => metadata is IAllowAnonymous);
    }

    [Theory]
    [InlineData(PaymentReconciliationStatus.Forbidden, 403)]
    [InlineData(PaymentReconciliationStatus.NotFound, 404)]
    [InlineData(PaymentReconciliationStatus.Failed, 400)]
    [InlineData(PaymentReconciliationStatus.Timeout, 504)]
    [InlineData(PaymentReconciliationStatus.Unavailable, 503)]
    [InlineData(PaymentReconciliationStatus.InvalidInput, 400)]
    [InlineData(PaymentReconciliationStatus.Ignored, 200)]
    [InlineData(PaymentReconciliationStatus.Retryable, 500)]
    [InlineData(PaymentReconciliationStatus.Success, 200)]
    [InlineData(PaymentReconciliationStatus.Duplicate, 200)]
    public void Statuses_map_to_http(PaymentReconciliationStatus status, int expected)
    {
        Assert.Equal(expected, PaymentHttpStatus.For(status));
    }

    private static RouteEndpoint[] Map()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<PaymentReconciliationService>();
        var app = builder.Build();
        app.MapPaymentEndpoints();
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
    }
}
