using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Tests;

public class PlatformLimitsContractTests
{
    [Fact]
    public void Response_mirrors_the_domain_constants()
    {
        var response = PlatformEndpoints.ToResponse();

        Assert.Equal(PlatformLimits.MinEmployeeLimit, response.MinEmployeeLimit);
        Assert.Equal(PlatformLimits.HardEmployeeCap, response.HardEmployeeCap);
        Assert.Equal(PlatformLimits.DefaultEmployeeLimit, response.DefaultEmployeeLimit);
        Assert.Equal(PlatformLimits.DefaultPlanName, response.DefaultPlanName);
        Assert.Equal(PlatformLimits.CurrencyCode, response.CurrencyCode);
    }

    [Fact]
    public void Response_serializes_with_web_camel_case()
    {
        var json = JsonSerializer.Serialize(
            PlatformEndpoints.ToResponse(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            PlatformLimits.HardEmployeeCap,
            document.RootElement.GetProperty("hardEmployeeCap").GetInt32());
        Assert.Equal(
            PlatformLimits.DefaultEmployeeLimit,
            document.RootElement.GetProperty("defaultEmployeeLimit").GetInt32());
        Assert.Equal(
            PlatformLimits.MinEmployeeLimit,
            document.RootElement.GetProperty("minEmployeeLimit").GetInt32());
    }

    [Fact]
    public void Registration_maps_an_anonymous_platform_route()
    {
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        app.MapPlatformEndpoints();

        var endpoint = Assert.Single(
            ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>());

        Assert.Equal("/api/platform", endpoint.RoutePattern.RawText);
        Assert.Contains(
            HttpMethods.Get,
            endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods);
        Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
    }
}
