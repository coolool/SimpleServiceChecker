using ClosedXML.Excel;
using SimpleServiceChecker.App.Infrastructure;
using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.App.Tests;

public sealed class WorkbookActivationServiceTests
{
    [Fact]
    public async Task ActivateAndReload_PersistsValidatedLocalCopyWithoutSourcePath()
    {
        using TestWorkbook fixture = TestWorkbook.Create();
        using TestDirectory directory = TestDirectory.Create();
        var service = directory.CreateService();

        var activation = await service.ActivateAsync(fixture.Path);
        var reloaded = await service.LoadCurrentAsync();

        Assert.True(activation.LoadResult.IsValid);
        Assert.NotNull(activation.Manifest);
        Assert.Equal(Path.GetFileName(fixture.Path), activation.Manifest.SourceFileName);
        Assert.DoesNotContain(Path.GetDirectoryName(fixture.Path)!, File.ReadAllText(directory.CurrentManifestPath), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(reloaded?.LoadResult.Dataset);
        _ = Assert.Single(reloaded.LoadResult.Dataset.Towers);
        _ = Assert.Single(reloaded.LoadResult.Dataset.FibreRanges);
    }

    [Fact]
    public async Task InvalidReplacement_DoesNotDisplaceCurrentWorkbook()
    {
        using TestWorkbook fixture = TestWorkbook.Create();
        using TestDirectory directory = TestDirectory.Create();
        var service = directory.CreateService();
        var accepted = await service.ActivateAsync(fixture.Path);
        var acceptedHash = accepted.Manifest!.Sha256Hex;
        var invalidPath = Path.Combine(directory.Root, "invalid.xlsx");
        await File.WriteAllTextAsync(invalidPath, "not a workbook");

        var rejected = await service.ActivateAsync(invalidPath);
        var reloaded = await service.LoadCurrentAsync();

        Assert.False(rejected.LoadResult.IsValid);
        Assert.Equal(acceptedHash, reloaded?.Manifest?.Sha256Hex);
        _ = Assert.Single(reloaded!.LoadResult.Dataset!.Towers);
    }

    [Fact]
    public async Task RestorePrevious_ReversesTheLastActivationAndSurvivesRestart()
    {
        using TestWorkbook first = TestWorkbook.Create("First Tower");
        using TestWorkbook second = TestWorkbook.Create("Second Tower");
        using TestDirectory directory = TestDirectory.Create();
        var service = directory.CreateService();

        _ = await service.ActivateAsync(first.Path);
        _ = await service.ActivateAsync(second.Path);
        var restored = await directory.CreateService().RestorePreviousAsync();
        var current = await directory.CreateService().LoadCurrentAsync();
        var previous = await directory.CreateService().LoadPreviousAsync();

        Assert.True(restored?.LoadResult.IsValid);
        Assert.Equal("First Tower", Assert.Single(current!.LoadResult.Dataset!.Towers).Name);
        Assert.Equal("Second Tower", Assert.Single(previous!.LoadResult.Dataset!.Towers).Name);
    }

    [Fact]
    public async Task CorruptCurrentWorkbook_RecoversLastValidPreviousCopyAtStartup()
    {
        using TestWorkbook first = TestWorkbook.Create("Recovery Tower");
        using TestWorkbook second = TestWorkbook.Create("Corruptible Tower");
        using TestDirectory directory = TestDirectory.Create();
        var service = directory.CreateService();
        _ = await service.ActivateAsync(first.Path);
        _ = await service.ActivateAsync(second.Path);
        await File.WriteAllTextAsync(directory.CurrentWorkbookPath, "interrupted replacement");

        var recovered = await directory.CreateService().LoadCurrentAsync();

        Assert.True(recovered?.RecoveredFromPrevious);
        Assert.Equal("Recovery Tower", Assert.Single(recovered!.LoadResult.Dataset!.Towers).Name);
    }

    [Fact]
    public async Task MissingSource_DoesNotDisplaceCurrentWorkbook()
    {
        using TestWorkbook fixture = TestWorkbook.Create("Still Active");
        using TestDirectory directory = TestDirectory.Create();
        var service = directory.CreateService();
        var accepted = await service.ActivateAsync(fixture.Path);

        var rejected = await service.ActivateAsync(Path.Combine(directory.Root, "moved-or-deleted.xlsx"));
        var reloaded = await service.LoadCurrentAsync();

        Assert.False(rejected.LoadResult.IsValid);
        Assert.Equal(accepted.Manifest!.Sha256Hex, reloaded!.Manifest!.Sha256Hex);
        Assert.Equal("Still Active", Assert.Single(reloaded.LoadResult.Dataset!.Towers).Name);
    }

    [Fact]
    public async Task ExclusivelyLockedSource_DoesNotDisplaceCurrentWorkbook()
    {
        using TestWorkbook active = TestWorkbook.Create("Active Tower");
        using TestWorkbook locked = TestWorkbook.Create("Locked Tower");
        using TestDirectory directory = TestDirectory.Create();
        var service = directory.CreateService();
        var accepted = await service.ActivateAsync(active.Path);
        await using var exclusiveLock = new FileStream(locked.Path, FileMode.Open, FileAccess.Read, FileShare.None);

        var rejected = await service.ActivateAsync(locked.Path);
        var reloaded = await service.LoadCurrentAsync();

        Assert.False(rejected.LoadResult.IsValid);
        Assert.Equal(accepted.Manifest!.Sha256Hex, reloaded!.Manifest!.Sha256Hex);
        Assert.Equal("Active Tower", Assert.Single(reloaded.LoadResult.Dataset!.Towers).Name);
    }

    [Fact]
    public async Task TamperedCurrentWithoutPrevious_IsRejectedInsteadOfTrusted()
    {
        using TestWorkbook active = TestWorkbook.Create("Original Tower");
        using TestDirectory directory = TestDirectory.Create();
        var service = directory.CreateService();
        _ = await service.ActivateAsync(active.Path);
        await File.AppendAllTextAsync(directory.CurrentWorkbookPath, "tampered");

        var loaded = await directory.CreateService().LoadCurrentAsync();

        Assert.NotNull(loaded);
        Assert.False(loaded.LoadResult.IsValid);
        Assert.Null(loaded.Manifest);
    }

    [Fact]
    public async Task InspectionCopy_IsReadOnlyAndCleanupRemovesItWhenClosed()
    {
        using TestWorkbook fixture = TestWorkbook.Create();
        using TestDirectory directory = TestDirectory.Create();
        var service = directory.CreateService();
        _ = await service.ActivateAsync(fixture.Path);

        var inspectionCopy = await service.CreateReadOnlyInspectionCopyAsync();

        Assert.NotNull(inspectionCopy);
        Assert.True(File.GetAttributes(inspectionCopy!).HasFlag(FileAttributes.ReadOnly));
        service.CleanupTemporaryFiles();
        Assert.False(File.Exists(inspectionCopy));
    }

    [Fact]
    public async Task ActivationWarnings_ArePersistedForLaterDisplay()
    {
        using TestWorkbook fixture = TestWorkbook.Create("Incomplete Tower", heightFeet: 0, rangeKilometres: 0);
        using TestDirectory directory = TestDirectory.Create();

        var activation = await directory.CreateService().ActivateAsync(fixture.Path);
        var reloaded = await directory.CreateService().LoadCurrentAsync();

        Assert.True(activation.LoadResult.IsValid);
        Assert.NotEmpty(activation.LoadResult.Warnings);
        Assert.Equal(activation.LoadResult.Warnings.Count, reloaded!.Manifest!.WarningCount);
        Assert.Equal(activation.LoadResult.Warnings.Select(issue => issue.Message), reloaded.Manifest.Warnings);
    }

    private sealed class TestDirectory : IDisposable
    {
        private TestDirectory(string root)
        {
            Root = root;
            CurrentWorkbookPath = Path.Combine(root, "coverage", "current.xlsx");
            CurrentManifestPath = Path.Combine(root, "coverage", "current-manifest.json");
            PreviousWorkbookPath = Path.Combine(root, "coverage", "previous.xlsx");
            PreviousManifestPath = Path.Combine(root, "coverage", "previous-manifest.json");
            TemporaryPath = Path.Combine(root, "temp");
        }

        public string Root { get; }
        public string CurrentWorkbookPath { get; }
        public string CurrentManifestPath { get; }
        public string PreviousWorkbookPath { get; }
        public string PreviousManifestPath { get; }
        public string TemporaryPath { get; }

        public static TestDirectory Create()
        {
            return new(Path.Combine(Path.GetTempPath(), $"ssc-activation-{Guid.NewGuid():N}"));
        }

        public WorkbookActivationService CreateService()
        {
            return new(CurrentWorkbookPath, CurrentManifestPath, PreviousWorkbookPath, PreviousManifestPath, TemporaryPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class TestWorkbook : IDisposable
    {
        private TestWorkbook(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestWorkbook Create(string towerName = "Test Tower", double heightFeet = 100, double rangeKilometres = 25)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ssc-app-{Guid.NewGuid():N}.xlsx");
            using XLWorkbook workbook = new XLWorkbook();
            var towers = workbook.AddWorksheet(WorkbookProbe.TowerSheetName);
            AddHeaders(towers, WorkbookProbe.TowerHeaders);
            SetRow(towers, 2, [towerName, heightFeet, rangeKilometres, 100d, 20d, null, 360d, 46.5d, -84.3d, null]);
            var fibre = workbook.AddWorksheet(WorkbookProbe.FibreSheetName);
            AddHeaders(fibre, WorkbookProbe.FibreHeaders);
            SetRow(fibre, 2, [100d, 200d, "Main", "Rd", "Sault Ste. Marie", "P6A 1A1", "150/150", null]);
            workbook.SaveAs(path);
            return new TestWorkbook(path);
        }

        public void Dispose()
        {
            File.Delete(Path);
        }

        private static void AddHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
        {
            for (var index = 0; index < headers.Count; index++)
            {
                sheet.Cell(1, index + 1).Value = headers[index];
            }
        }

        private static void SetRow(IXLWorksheet sheet, int row, IReadOnlyList<object?> values)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index] is not null)
                {
                    sheet.Cell(row, index + 1).Value = XLCellValue.FromObject(values[index]);
                }
            }
        }
    }
}
