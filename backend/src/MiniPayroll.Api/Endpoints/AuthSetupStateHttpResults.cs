namespace MiniPayroll.Api.Endpoints;

public static class AuthSetupStateHttpResults
{
    public static IResult Forbidden() =>
        Results.Json(
            new { error = "The company setup state is unavailable." },
            statusCode: StatusCodes.Status403Forbidden);
}
