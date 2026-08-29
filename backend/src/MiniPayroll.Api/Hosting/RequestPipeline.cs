namespace MiniPayroll.Api.Hosting;

public enum RequestPipelineStage
{
    HttpsRedirection,
    UploadedFiles,
    Cors,
    Authentication,
    Authorization
}

public static class RequestPipeline
{
    public static IReadOnlyList<RequestPipelineStage> Stages(bool isDevelopment) =>
        isDevelopment
            ?
            [
                RequestPipelineStage.UploadedFiles,
                RequestPipelineStage.Cors,
                RequestPipelineStage.Authentication,
                RequestPipelineStage.Authorization
            ]
            :
            [
                RequestPipelineStage.HttpsRedirection,
                RequestPipelineStage.UploadedFiles,
                RequestPipelineStage.Cors,
                RequestPipelineStage.Authentication,
                RequestPipelineStage.Authorization
            ];

    public static WebApplication Configure(
        WebApplication app,
        StaticFileOptions uploadedFiles,
        string corsPolicy)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(uploadedFiles);

        foreach (var stage in Stages(app.Environment.IsDevelopment()))
        {
            switch (stage)
            {
                case RequestPipelineStage.HttpsRedirection:
                    app.UseHttpsRedirection();
                    break;
                case RequestPipelineStage.UploadedFiles:
                    app.UseStaticFiles(uploadedFiles);
                    break;
                case RequestPipelineStage.Cors:
                    app.UseCors(corsPolicy);
                    break;
                case RequestPipelineStage.Authentication:
                    app.UseAuthentication();
                    break;
                case RequestPipelineStage.Authorization:
                    app.UseAuthorization();
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled pipeline stage {stage}.");
            }
        }

        return app;
    }
}
