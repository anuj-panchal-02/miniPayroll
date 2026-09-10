using System.Text.Json;
using MiniPayroll.Api.Endpoints;

namespace MiniPayroll.Tests;

public class CompanyAdminResponseContractTests
{
    [Fact]
    public void Create_admin_response_does_not_serialize_a_password()
    {
        var response = new CompanyEndpoints.CreateAdminResponse(Guid.NewGuid(), "owner@abctraders.example");
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        Assert.Equal("owner@abctraders.example", document.RootElement.GetProperty("email").GetString());
        Assert.False(document.RootElement.TryGetProperty("temporaryPassword", out _));
    }
}
