using Microsoft.Extensions.Logging.Abstractions;
using MiniPayroll.Api.Endpoints;
using MiniPayroll.Api.Storage;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class CompanyLogoUploadCoordinatorTests
{
    [Fact]
    public async Task Save_uses_company_id_from_service_state()
    {
        var state = State(Guid.NewGuid());
        var storage = new RecordingStorage();
        var coordinator = CreateCoordinator(storage);
        await using var content = new MemoryStream(PngBytes());

        await coordinator.SaveAsync(
            state,
            content,
            "logo.png",
            (path, _) => Task.FromResult(Success(state, path)));

        Assert.Equal(state.CompanyId, storage.SavedCompanyId);
    }

    [Fact]
    public async Task Database_failure_deletes_new_file()
    {
        var state = State(Guid.NewGuid(), "companies/tenant/old.png");
        var storage = new RecordingStorage();
        var coordinator = CreateCoordinator(storage);
        await using var content = new MemoryStream(PngBytes());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.SaveAsync(
                state,
                content,
                "logo.png",
                (_, _) => throw new InvalidOperationException("database failed")));

        Assert.Equal([storage.NewPath], storage.DeletedPaths);
    }

    [Fact]
    public async Task Successful_persistence_deletes_prior_file_after_database_save()
    {
        const string oldPath = "companies/tenant/old.png";
        var events = new List<string>();
        var state = State(Guid.NewGuid(), oldPath);
        var storage = new RecordingStorage(events);
        var coordinator = CreateCoordinator(storage);
        await using var content = new MemoryStream(PngBytes());

        var result = await coordinator.SaveAsync(
            state,
            content,
            "logo.png",
            (path, _) =>
            {
                events.Add("persist");
                return Task.FromResult(Success(state, path));
            });

        Assert.Equal(CompanySetupStatus.Success, result.Status);
        Assert.Equal(["save", "persist", $"delete:{oldPath}"], events);
    }

    [Fact]
    public async Task Completed_setup_is_rejected_before_any_file_is_written()
    {
        var state = State(Guid.NewGuid(), "companies/tenant/old.png") with
        {
            IsSetupComplete = true,
            SetupStep = CompanySetupStep.Complete
        };
        var storage = new RecordingStorage();
        var coordinator = CreateCoordinator(storage);
        await using var content = new MemoryStream(PngBytes());

        var result = await coordinator.SaveAsync(
            state,
            content,
            "logo.png",
            (_, _) => throw new InvalidOperationException("persistence must not be attempted"));

        Assert.Equal(CompanySetupStatus.AlreadyComplete, result.Status);
        Assert.Null(storage.SavedCompanyId);
        Assert.Empty(storage.DeletedPaths);
    }

    private static CompanyLogoUploadCoordinator CreateCoordinator(
        ICompanyLogoStorage storage) =>
        new(storage, NullLogger<CompanyLogoUploadCoordinator>.Instance);

    private static CompanySetupResult Success(
        CompanySetupState state,
        string path) =>
        new(CompanySetupStatus.Success, state with { LogoPath = path });

    private static CompanySetupState State(
        Guid companyId,
        string? logoPath = null) =>
        new(
            companyId,
            "ABC Traders",
            "owner@example.com",
            "1234567890",
            "Main Road",
            null,
            "Pune",
            "Maharashtra",
            "411001",
            logoPath,
            DailyRateMethod.CalendarDays,
            26,
            ["Sunday"],
            CompanySetupStep.Review,
            false);

    private static byte[] PngBytes() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private sealed class RecordingStorage(List<string>? events = null) : ICompanyLogoStorage
    {
        public string NewPath { get; } = "companies/tenant/new.png";

        public Guid? SavedCompanyId { get; private set; }

        public List<string> DeletedPaths { get; } = [];

        public Task<StoredCompanyLogo> SaveAsync(
            Guid tenantCompanyId,
            Stream content,
            string? originalFileName,
            CancellationToken cancellationToken = default)
        {
            SavedCompanyId = tenantCompanyId;
            events?.Add("save");
            return Task.FromResult(new StoredCompanyLogo(NewPath));
        }

        public Task DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            DeletedPaths.Add(relativePath);
            events?.Add($"delete:{relativePath}");
            return Task.CompletedTask;
        }
    }
}
