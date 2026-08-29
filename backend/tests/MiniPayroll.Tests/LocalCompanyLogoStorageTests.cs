using Microsoft.Extensions.Options;
using MiniPayroll.Api.Storage;

namespace MiniPayroll.Tests;

public sealed class LocalCompanyLogoStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "miniPayroll-logo-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Save_generates_tenant_scoped_path_from_png_signature()
    {
        var companyId = Guid.NewGuid();
        var storage = CreateStorage();
        await using var content = new MemoryStream(PngBytes());

        var stored = await storage.SaveAsync(companyId, content, "misleading.jpg");

        Assert.StartsWith($"companies/{companyId:D}/", stored.RelativePath);
        Assert.EndsWith(".png", stored.RelativePath);
        Assert.True(File.Exists(Path.Combine(
            _root,
            stored.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Theory]
    [MemberData(nameof(SupportedImages))]
    public async Task Save_accepts_only_supported_image_signatures(
        byte[] bytes,
        string expectedExtension)
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream(bytes);

        var stored = await storage.SaveAsync(Guid.NewGuid(), content, "logo.bin");

        Assert.EndsWith(expectedExtension, stored.RelativePath);
    }

    [Fact]
    public async Task Save_rejects_extension_spoofing()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream("not an image"u8.ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.SaveAsync(Guid.NewGuid(), content, "logo.png"));

        Assert.Empty(Directory.Exists(_root)
            ? Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public async Task Failed_replacement_keeps_previously_saved_file()
    {
        var storage = CreateStorage(maxBytes: 16);
        var companyId = Guid.NewGuid();
        await using var firstContent = new MemoryStream(PngBytes());
        var first = await storage.SaveAsync(companyId, firstContent, "first.png");
        var firstPath = Path.Combine(
            _root,
            first.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        await using var oversized = new MemoryStream(PngBytes().Concat(new byte[16]).ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.SaveAsync(companyId, oversized, "replacement.png"));

        Assert.True(File.Exists(firstPath));
        Assert.Single(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Delete_removes_saved_file()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream(PngBytes());
        var stored = await storage.SaveAsync(Guid.NewGuid(), content, "logo.png");

        await storage.DeleteAsync(stored.RelativePath);

        Assert.False(File.Exists(Path.Combine(
            _root,
            stored.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task Delete_rejects_paths_outside_configured_root()
    {
        var outsidePath = Path.Combine(Path.GetDirectoryName(_root)!, "outside.png");
        Directory.CreateDirectory(Path.GetDirectoryName(outsidePath)!);
        await File.WriteAllBytesAsync(outsidePath, PngBytes());
        var storage = CreateStorage();

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.DeleteAsync("../outside.png"));

        Assert.True(File.Exists(outsidePath));
        File.Delete(outsidePath);
    }

    public static TheoryData<byte[], string> SupportedImages() => new()
    {
        { PngBytes(), ".png" },
        { [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0], ".jpg" },
        { "RIFF0000WEBP"u8.ToArray(), ".webp" }
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalCompanyLogoStorage CreateStorage(long maxBytes = 2 * 1024 * 1024) =>
        new(Options.Create(new CompanyLogoStorageOptions
        {
            RootPath = _root,
            MaxLogoBytes = maxBytes
        }));

    private static byte[] PngBytes() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
}
