namespace MiniPayroll.Api.Storage;

public sealed class CompanyLogoStorageOptions
{
    public string RootPath { get; set; } = "uploads";

    public long MaxLogoBytes { get; set; } = 2 * 1024 * 1024;
}
