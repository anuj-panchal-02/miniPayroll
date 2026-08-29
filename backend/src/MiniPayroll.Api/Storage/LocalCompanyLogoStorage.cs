using Microsoft.Extensions.Options;

namespace MiniPayroll.Api.Storage;

public sealed class LocalCompanyLogoStorage : ICompanyLogoStorage
{
    private const int SignatureLength = 12;
    private readonly string _rootPath;
    private readonly long _maxLogoBytes;

    public LocalCompanyLogoStorage(IOptions<CompanyLogoStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Value.RootPath))
        {
            throw new InvalidOperationException("FileStorage:RootPath must be configured.");
        }

        if (options.Value.MaxLogoBytes <= 0)
        {
            throw new InvalidOperationException("FileStorage:MaxLogoBytes must be greater than zero.");
        }

        _rootPath = Path.GetFullPath(options.Value.RootPath);
        _maxLogoBytes = options.Value.MaxLogoBytes;
    }

    public async Task<StoredCompanyLogo> SaveAsync(
        Guid tenantCompanyId,
        Stream content,
        string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var buffered = new MemoryStream();
        var copyBuffer = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(copyBuffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffered.Length + read > _maxLogoBytes)
            {
                throw new InvalidDataException(
                    $"Company logo must not exceed {_maxLogoBytes} bytes.");
            }

            await buffered.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken);
        }

        var extension = DetectExtension(buffered.GetBuffer().AsSpan(0, checked((int)buffered.Length)));
        var relativePath = Path.Combine(
                "companies",
                tenantCompanyId.ToString("D"),
                $"{Guid.NewGuid():N}{extension}")
            .Replace(Path.DirectorySeparatorChar, '/');
        var fullPath = ResolveSafePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        buffered.Position = 0;
        await using var destination = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await buffered.CopyToAsync(destination, cancellationToken);

        return new StoredCompanyLogo(relativePath);
    }

    public Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveSafePath(relativePath);
        File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveSafePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("A relative storage path is required.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(
            relativePath.Replace('/', Path.DirectorySeparatorChar),
            _rootPath);
        var rootWithSeparator = _rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(rootWithSeparator, pathComparison))
        {
            throw new ArgumentException(
                "The storage path must remain inside the configured root.",
                nameof(relativePath));
        }

        return fullPath;
    }

    private static string DetectExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8
            && bytes[..8].SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }

        if (bytes.Length >= 3
            && bytes[0] == 0xFF
            && bytes[1] == 0xD8
            && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= SignatureLength
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        throw new InvalidDataException("Only PNG, JPEG, and WebP company logos are supported.");
    }
}
