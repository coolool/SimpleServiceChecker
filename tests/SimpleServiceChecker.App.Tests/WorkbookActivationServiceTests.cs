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
        Assert.DoesNotContain(Path.GetDirectoryName(fixture.Path)!, File.ReadAllText(directory.ManifestPath), StringComparison.OrdinalIgnoreCase);
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

    private sealed class TestDirectory : IDisposable
    {
        private TestDirectory(string root)
        {
            Root = root;
            WorkbookPath = Path.Combine(root, "coverage", "current.xlsx");
            ManifestPath = Path.Combine(root, "coverage", "current-manifest.json");
        }

        public string Root { get; }
        public string WorkbookPath { get; }
        public string ManifestPath { get; }

        public static TestDirectory Create()
        {
            return new(Path.Combine(Path.GetTempPath(), $"ssc-activation-{Guid.NewGuid():N}"));
        }

        public WorkbookActivationService CreateService()
        {
            return new(WorkbookPath, ManifestPath, Path.Combine(Root, "temp"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
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

        public static TestWorkbook Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ssc-app-{Guid.NewGuid():N}.xlsx");
            using XLWorkbook workbook = new XLWorkbook();
            var towers = workbook.AddWorksheet(WorkbookProbe.TowerSheetName);
            AddHeaders(towers, WorkbookProbe.TowerHeaders);
            SetRow(towers, 2, ["Test Tower", 100d, 25d, 100d, 20d, null, 360d, 46.5d, -84.3d, null]);
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
