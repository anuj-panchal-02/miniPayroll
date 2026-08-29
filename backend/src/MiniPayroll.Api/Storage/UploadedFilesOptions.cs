using Microsoft.Extensions.FileProviders;

namespace MiniPayroll.Api.Storage;

public static class UploadedFilesOptions
{
    public const string RequestPath = "/uploads";
    public const string ContentTypeOptionsHeader = "X-Content-Type-Options";
    public const string NoSniff = "nosniff";

    public static StaticFileOptions Create(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("An upload root path is required.", nameof(rootPath));
        }

        Directory.CreateDirectory(rootPath);

        return new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(rootPath),
            RequestPath = RequestPath,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = static context =>
                context.Context.Response.Headers[ContentTypeOptionsHeader] = NoSniff
        };
    }
}
