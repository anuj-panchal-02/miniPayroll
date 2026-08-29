using Microsoft.Extensions.Options;
using MiniPayroll.Api.Auth;
using MiniPayroll.Api.Storage;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class CompanySetupEndpoints
{
    public static IEndpointRouteBuilder MapCompanySetupEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/company/setup")
            .RequireAuthorization(CompanySetupAuthorization.Configure);

        group.MapGet(string.Empty, Get);
        group.MapPatch("/details", UpdateDetails);
        group.MapPatch("/payroll-settings", UpdatePayrollSettings);
        group.MapPost("/logo", UploadLogo);
        group.MapPost("/complete", Complete);

        return routes;
    }

    private static async Task<IResult> Get(
        CompanySetupService setup,
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ToHttpResult(await setup.GetAsync(cancellationToken), request);

    private static async Task<IResult> UpdateDetails(
        CompanyDetailsInput? input,
        CompanySetupService setup,
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ToHttpResult(
            await setup.UpdateDetailsAsync(input, cancellationToken),
            request);

    private static async Task<IResult> UpdatePayrollSettings(
        PayrollSettingsInput? input,
        CompanySetupService setup,
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ToHttpResult(
            await setup.UpdatePayrollSettingsAsync(input, cancellationToken),
            request);

    private static async Task<IResult> UploadLogo(
        HttpRequest request,
        CompanySetupService setup,
        CompanyLogoUploadCoordinator logoUpload,
        IOptions<CompanyLogoStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var current = await setup.GetAsync(cancellationToken);
        if (current.Status != CompanySetupStatus.Success)
        {
            return ToHttpResult(current, request);
        }

        if (current.State is not { } currentState)
        {
            return Error(CompanySetupStatus.CompanyNotFound, null, request);
        }

        if (currentState.IsSetupComplete)
        {
            return Error(CompanySetupStatus.AlreadyComplete, currentState, request);
        }

        if (CompanyLogoUploadLimits.IsMultipartBodyTooLarge(
                request.ContentLength,
                storageOptions.Value.MaxLogoBytes))
        {
            return Error(
                CompanySetupStatus.InvalidInput,
                current.State,
                request,
                "The multipart logo request is too large.");
        }

        if (!request.HasFormContentType)
        {
            return Error(
                CompanySetupStatus.InvalidInput,
                current.State,
                request,
                "A multipart form with a 'file' field is required.");
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return Error(
                CompanySetupStatus.InvalidInput,
                current.State,
                request,
                exception.Message);
        }

        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Error(
                CompanySetupStatus.InvalidInput,
                current.State,
                request,
                "A non-empty 'file' field is required.");
        }

        if (CompanyLogoUploadLimits.IsFileTooLarge(
                file.Length,
                storageOptions.Value.MaxLogoBytes))
        {
            return Error(
                CompanySetupStatus.InvalidInput,
                current.State,
                request,
                $"Company logo must not exceed {storageOptions.Value.MaxLogoBytes} bytes.");
        }

        CompanySetupResult persisted;
        try
        {
            await using var stream = file.OpenReadStream();
            persisted = await logoUpload.SaveAsync(
                currentState,
                stream,
                file.FileName,
                setup.SaveLogoPathAsync,
                cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return Error(
                CompanySetupStatus.InvalidInput,
                current.State,
                request,
                exception.Message);
        }

        return ToHttpResult(persisted, request);
    }

    private static async Task<IResult> Complete(
        CompanySetupService setup,
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ToHttpResult(await setup.CompleteAsync(cancellationToken), request);

    private static IResult ToHttpResult(
        CompanySetupResult result,
        HttpRequest request)
    {
        var response = result.State is null
            ? null
            : ToResponse(result.State, request);
        if (result.Status == CompanySetupStatus.Success)
        {
            return Results.Ok(response);
        }

        return Error(result.Status, result.State, request);
    }

    private static IResult Error(
        CompanySetupStatus status,
        CompanySetupState? state,
        HttpRequest request,
        string? message = null) =>
        Results.Json(
            new CompanySetupError(
                message ?? ErrorMessage(status),
                state is null ? null : ToResponse(state, request)),
            statusCode: CompanySetupHttpStatus.For(status));

    private static CompanySetupResponse ToResponse(
        CompanySetupState state,
        HttpRequest request) =>
        new(
            state.Name,
            state.ContactEmail,
            state.ContactPhone,
            state.AddressLine1,
            state.AddressLine2,
            state.City,
            state.State,
            state.PostalCode,
            BuildLogoUrl(state.LogoPath, request),
            "Monthly",
            state.DailyRateMethod.ToString(),
            state.WorkingDaysPerMonth,
            state.WeeklyOffDays,
            state.SetupStep.ToString(),
            state.IsSetupComplete);

    private static string? BuildLogoUrl(string? logoPath, HttpRequest request)
    {
        var relativePath = NormalizeStoragePath(logoPath);
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        return $"{request.PathBase}/uploads/{relativePath}";
    }

    private static string? NormalizeStoragePath(string? path)
    {
        var normalized = path?.Trim().Replace('\\', '/').TrimStart('/');
        return normalized?.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) == true
            ? normalized["uploads/".Length..]
            : normalized;
    }

    private static string ErrorMessage(CompanySetupStatus status) =>
        status switch
        {
            CompanySetupStatus.InvalidInput => "The company setup input is invalid.",
            CompanySetupStatus.CompanyNotFound => "The tenant company was not found.",
            CompanySetupStatus.InvalidStep => "The company setup step is not available yet.",
            CompanySetupStatus.AlreadyComplete => "Company setup is already complete.",
            CompanySetupStatus.Conflict =>
                "Company setup changed in another request. Reload and try again.",
            _ => "The company setup request could not be completed."
        };

    public sealed record CompanySetupResponse(
        string Name,
        string ContactEmail,
        string? ContactPhone,
        string? AddressLine1,
        string? AddressLine2,
        string? City,
        string? State,
        string? PostalCode,
        string? LogoUrl,
        string PayrollCycle,
        string DailyRateMethod,
        int WorkingDaysPerMonth,
        IReadOnlyList<string> WeeklyOffDays,
        string SetupStep,
        bool IsSetupComplete);

    public sealed record CompanySetupError(
        string Error,
        CompanySetupResponse? State);
}

public static class CompanySetupHttpStatus
{
    public static int For(CompanySetupStatus status) =>
        status switch
        {
            CompanySetupStatus.Success => StatusCodes.Status200OK,
            CompanySetupStatus.InvalidInput => StatusCodes.Status400BadRequest,
            CompanySetupStatus.CompanyNotFound => StatusCodes.Status404NotFound,
            CompanySetupStatus.InvalidStep
                or CompanySetupStatus.AlreadyComplete
                or CompanySetupStatus.Conflict =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
}

public static class CompanyLogoUploadLimits
{
    private const long MultipartOverheadBytes = 32 * 1024;

    public static long MultipartBodyLengthLimit(long maxLogoBytes) =>
        checked(maxLogoBytes + MultipartOverheadBytes);

    public static bool IsMultipartBodyTooLarge(
        long? contentLength,
        long maxLogoBytes) =>
        contentLength > MultipartBodyLengthLimit(maxLogoBytes);

    public static bool IsFileTooLarge(long fileLength, long maxLogoBytes) =>
        fileLength > maxLogoBytes;
}
