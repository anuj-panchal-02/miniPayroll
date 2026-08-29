using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using MiniPayroll.Api.Hosting;
using MiniPayroll.Api.Storage;

namespace MiniPayroll.Tests;

public sealed class UploadedFilesPipelineTests
{
    [Fact]
    public void Https_redirection_runs_before_uploaded_files_outside_development()
    {
        var stages = RequestPipeline.Stages(isDevelopment: false).ToList();

        Assert.Contains(RequestPipelineStage.HttpsRedirection, stages);
        Assert.True(
            stages.IndexOf(RequestPipelineStage.HttpsRedirection)
                < stages.IndexOf(RequestPipelineStage.UploadedFiles),
            "HTTPS redirection must run before uploaded files are served.");
    }

    [Fact]
    public void Development_serves_uploaded_files_without_https_redirection()
    {
        var stages = RequestPipeline.Stages(isDevelopment: true).ToList();

        Assert.DoesNotContain(RequestPipelineStage.HttpsRedirection, stages);
        Assert.Equal(RequestPipelineStage.UploadedFiles, stages[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cors_and_authorization_keep_their_relative_order(bool isDevelopment)
    {
        var stages = RequestPipeline.Stages(isDevelopment).ToList();

        Assert.True(
            stages.IndexOf(RequestPipelineStage.UploadedFiles)
                < stages.IndexOf(RequestPipelineStage.Cors));
        Assert.True(
            stages.IndexOf(RequestPipelineStage.Cors)
                < stages.IndexOf(RequestPipelineStage.Authentication));
        Assert.True(
            stages.IndexOf(RequestPipelineStage.Authentication)
                < stages.IndexOf(RequestPipelineStage.Authorization));
    }

    [Fact]
    public void Uploaded_file_responses_are_not_content_type_sniffed()
    {
        using var root = new TemporaryDirectory();
        var options = UploadedFilesOptions.Create(root.Path);
        var context = new DefaultHttpContext();

        Assert.NotNull(options.OnPrepareResponse);
        options.OnPrepareResponse(
            new StaticFileResponseContext(context, new NotFoundFileInfo("logo.png")));

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
    }

    [Fact]
    public void Uploaded_files_are_served_from_the_configured_root_under_uploads()
    {
        using var root = new TemporaryDirectory();

        var options = UploadedFilesOptions.Create(root.Path);

        Assert.Equal("/uploads", options.RequestPath.Value);
        Assert.False(options.ServeUnknownFileTypes);
        var provider = Assert.IsType<PhysicalFileProvider>(options.FileProvider);
        Assert.Equal(
            root.Path,
            provider.Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    [Fact]
    public void Missing_upload_root_is_created_so_static_files_can_be_served()
    {
        using var root = new TemporaryDirectory();
        var nested = Path.Combine(root.Path, "uploads");

        UploadedFilesOptions.Create(nested);

        Assert.True(Directory.Exists(nested));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() =>
            Path = Directory.CreateTempSubdirectory("minipayroll-uploads").FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
