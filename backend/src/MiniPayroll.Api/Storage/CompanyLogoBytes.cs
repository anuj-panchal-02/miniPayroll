namespace MiniPayroll.Api.Storage;

public static class CompanyLogoBytes
{
    public static byte[]? TryRead(string? relativePath, CompanyLogoStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(options.RootPath))
        {
            return null;
        }

        try
        {
            var root = Path.GetFullPath(options.RootPath);
            var fullPath = Path.GetFullPath(
                relativePath.Replace('/', Path.DirectorySeparatorChar),
                root);
            var rootWithSeparator = root.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(rootWithSeparator, comparison) || !File.Exists(fullPath))
            {
                return null;
            }

            return File.ReadAllBytes(fullPath);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
