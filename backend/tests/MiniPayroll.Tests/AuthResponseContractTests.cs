using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MiniPayroll.Api.Endpoints;

namespace MiniPayroll.Tests;

public class AuthResponseContractTests
{
    [Fact]
    public void Login_response_serializes_setup_state_with_web_camel_case()
    {
        var response = new AuthEndpoints.AuthResponse(
            "token",
            "admin@example.com",
            ["CompanyAdmin"],
            Guid.NewGuid(),
            MustChangePassword: true,
            RequiresMfaEnrollment: false,
            IsSetupComplete: false,
            SetupStep: "CompanyDetails");

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.GetProperty("isSetupComplete").GetBoolean());
        Assert.Equal(
            "CompanyDetails",
            document.RootElement.GetProperty("setupStep").GetString());
    }

    [Fact]
    public void Me_response_serializes_setup_state_with_web_camel_case()
    {
        var response = new AuthEndpoints.MeResponse(
            Guid.NewGuid(),
            "admin@example.com",
            ["Superadmin"],
            CompanyId: null,
            MustChangePassword: false,
            TwoFactorEnabled: true,
            IsSetupComplete: true,
            SetupStep: "Complete");

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.GetProperty("isSetupComplete").GetBoolean());
        Assert.Equal("Complete", document.RootElement.GetProperty("setupStep").GetString());
    }

    [Fact]
    public void Setup_state_unavailable_result_is_forbidden()
    {
        var result = AuthSetupStateHttpResults.Forbidden();

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }
}
