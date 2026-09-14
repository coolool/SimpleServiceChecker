using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.Core.Tests;

public sealed class CoverageWorkbookReaderTests
{
    [Fact]
    public void Load_ParsesReorderedTrimmedHeadersAndTextNumbers()
    {
        var towerHeaders = WorkbookProbe.TowerHeaders.Reverse().ToArray();
        towerHeaders[Array.IndexOf(towerHeaders, "Tower Name")] = "  Tower Name  ";
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(
            towerHeaders,
            configure: workbook =>
            {
                var sheet = workbook.Worksheet(WorkbookProbe.TowerSheetName);
                CoverageWorkbookTestFile.Set(sheet, 2, "Tower Height", " 125 ");
                CoverageWorkbookTestFile.Set(sheet, 2, "AP Coverage (°)", "360°");
            });

        var result = CoverageWorkbookReader.Load(file.Path);

        Assert.True(result.IsValid, JoinIssues(result));
        var tower = Assert.Single(result.Dataset!.Towers);
        Assert.Equal(2, tower.SourceRowNumber);
        Assert.Equal(125, tower.HeightFeet);
        Assert.True(tower.IsOmnidirectional);
        Assert.Null(tower.ApDirectionDegrees);
        Assert.Equal(Path.GetFileName(file.Path), result.Dataset.SourceFileName);
        _ = Assert.Single(result.Dataset.FibreRanges);
    }

    [Fact]
    public void Load_ReportsMissingSheetAndRequiredHeader()
    {
        using CoverageWorkbookTestFile missingSheet = CoverageWorkbookTestFile.Create(
            configure: workbook => workbook.Worksheet(WorkbookProbe.FibreSheetName).Delete());
        using CoverageWorkbookTestFile missingHeader = CoverageWorkbookTestFile.Create(
            towerHeaders: [.. WorkbookProbe.TowerHeaders.Where(header => header != "Lat")]);

        var sheetResult = CoverageWorkbookReader.Load(missingSheet.Path);
        var headerResult = CoverageWorkbookReader.Load(missingHeader.Path);

        Assert.Contains(sheetResult.Errors, issue => issue.Code == WorkbookValidationCode.MissingSheet && issue.SheetName == WorkbookProbe.FibreSheetName);
        Assert.Contains(headerResult.Errors, issue => issue.Code == WorkbookValidationCode.MissingHeader && issue.ColumnName == "Lat");
    }

    [Fact]
    public void Load_ReportsDuplicateHeaders()
    {
        var headers = WorkbookProbe.TowerHeaders.Concat(["Tower Name"]).ToArray();
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(towerHeaders: headers);

        var result = CoverageWorkbookReader.Load(file.Path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.Code == WorkbookValidationCode.DuplicateHeader && issue.ColumnName == "Tower Name");
    }

    [Fact]
    public void Load_ReportsDuplicateNormalizedSheetNames()
    {
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(configure: workbook =>
            workbook.AddWorksheet($"{WorkbookProbe.TowerSheetName} "));

        var result = CoverageWorkbookReader.Load(file.Path);

        Assert.Contains(result.Errors, issue => issue.Code == WorkbookValidationCode.DuplicateSheet && issue.SheetName == WorkbookProbe.TowerSheetName);
    }

    [Fact]
    public void Load_ReportsBlankRequiredValue()
    {
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(configure: workbook =>
            CoverageWorkbookTestFile.Set(workbook.Worksheet(WorkbookProbe.TowerSheetName), 2, "Tower Name", null));

        var result = CoverageWorkbookReader.Load(file.Path);

        Assert.Contains(result.Errors, issue =>
            issue.Code == WorkbookValidationCode.RequiredValueMissing &&
            issue.RowNumber == 2 &&
            issue.ColumnName == "Tower Name");
    }

    [Fact]
    public void Load_ReportsOriginalRowAndColumnForInvalidValues()
    {
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(configure: workbook =>
        {
            var sheet = workbook.Worksheet(WorkbookProbe.TowerSheetName);
            CoverageWorkbookTestFile.SetTowerRow(sheet, 4);
            CoverageWorkbookTestFile.Set(sheet, 4, "Lat", "north");
        });

        var result = CoverageWorkbookReader.Load(file.Path);

        var issue = Assert.Single(result.Errors, issue => issue.Code == WorkbookValidationCode.InvalidNumber);
        Assert.Equal(4, issue.RowNumber);
        Assert.Equal("Lat", issue.ColumnName);
        Assert.Contains("row 4", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_AcceptsCoordinateBoundariesAndWarnsThatZeroDisablesCandidate()
    {
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(configure: workbook =>
        {
            var sheet = workbook.Worksheet(WorkbookProbe.TowerSheetName);
            CoverageWorkbookTestFile.Set(sheet, 2, "Tower Height", 0d);
            CoverageWorkbookTestFile.Set(sheet, 2, "Tower Range", 0d);
            CoverageWorkbookTestFile.Set(sheet, 2, "Lat", -90d);
            CoverageWorkbookTestFile.Set(sheet, 2, "lon", 180d);
        });

        var result = CoverageWorkbookReader.Load(file.Path);

        Assert.True(result.IsValid, JoinIssues(result));
        Assert.Equal(2, result.Warnings.Count);
        Assert.All(result.Warnings, warning => Assert.Equal(WorkbookValidationCode.ZeroDisablesCandidate, warning.Code));
        var tower = Assert.Single(result.Dataset!.Towers);
        Assert.False(tower.CanProduceCandidate);
        Assert.Equal(-90, tower.Location.Latitude);
        Assert.Equal(180, tower.Location.Longitude);
    }

    [Theory]
    [InlineData("Lat", -90.0001)]
    [InlineData("Lat", 90.0001)]
    [InlineData("lon", -180.0001)]
    [InlineData("lon", 180.0001)]
    public void Load_RejectsCoordinatesOutsideBoundaries(string column, double value)
    {
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(configure: workbook =>
            CoverageWorkbookTestFile.Set(workbook.Worksheet(WorkbookProbe.TowerSheetName), 2, column, value));

        var result = CoverageWorkbookReader.Load(file.Path);

        Assert.Contains(result.Errors, issue => issue.Code == WorkbookValidationCode.NumberOutOfRange && issue.ColumnName == column);
    }

    [Fact]
    public void Load_AllowsBlankDirectionFor360ButRequiresItForASector()
    {
        using CoverageWorkbookTestFile omnidirectional = CoverageWorkbookTestFile.Create();
        using CoverageWorkbookTestFile sector = CoverageWorkbookTestFile.Create(configure: workbook =>
            CoverageWorkbookTestFile.Set(workbook.Worksheet(WorkbookProbe.TowerSheetName), 2, "AP Coverage (°)", 90d));

        var omniResult = CoverageWorkbookReader.Load(omnidirectional.Path);
        var sectorResult = CoverageWorkbookReader.Load(sector.Path);

        Assert.True(omniResult.IsValid, JoinIssues(omniResult));
        Assert.Contains(sectorResult.Errors, issue => issue.Code == WorkbookValidationCode.DirectionRequired);
    }

    [Fact]
    public void Load_ParsesOpenEndedFibreRangeAndKeepsSpeedText()
    {
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(configure: workbook =>
        {
            var sheet = workbook.Worksheet(WorkbookProbe.FibreSheetName);
            CoverageWorkbookTestFile.Set(sheet, 2, "Start_Civic_Num", " 100 ");
            CoverageWorkbookTestFile.Set(sheet, 2, "End_Civic_Num", " * ");
            CoverageWorkbookTestFile.Set(sheet, 2, "Max_Speeds", " 150/150 ");
        });

        var result = CoverageWorkbookReader.Load(file.Path);

        Assert.True(result.IsValid, JoinIssues(result));
        var fibre = Assert.Single(result.Dataset!.FibreRanges);
        Assert.Equal(100, fibre.StartCivicNumber);
        Assert.Null(fibre.EndCivicNumber);
        Assert.Equal("150/150", fibre.MaxSpeeds);
        Assert.True(fibre.Contains(int.MaxValue));
    }

    [Fact]
    public void Load_RejectsReversedFibreRangeAndInvalidSpeedText()
    {
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create(configure: workbook =>
        {
            var sheet = workbook.Worksheet(WorkbookProbe.FibreSheetName);
            CoverageWorkbookTestFile.Set(sheet, 2, "Start_Civic_Num", 200d);
            CoverageWorkbookTestFile.Set(sheet, 2, "End_Civic_Num", 100d);
            CoverageWorkbookTestFile.Set(sheet, 2, "Max_Speeds", "very fast");
        });

        var result = CoverageWorkbookReader.Load(file.Path);

        Assert.Contains(result.Errors, issue => issue.Code == WorkbookValidationCode.InvalidCivicRange);
        Assert.Contains(result.Errors, issue => issue.Code == WorkbookValidationCode.InvalidSpeedFormat);
    }

    [Fact]
    public async Task LoadAsync_ObservesCancellation()
    {
        using CoverageWorkbookTestFile file = CoverageWorkbookTestFile.Create();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CoverageWorkbookReader.LoadAsync(file.Path, cancellation.Token));
    }

    [Fact]
    public void Load_ReturnsSafeErrorForMissingFile()
    {
        var result = CoverageWorkbookReader.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.xlsx"));

        var issue = Assert.Single(result.Errors);
        Assert.Equal(WorkbookValidationCode.FileReadFailed, issue.Code);
        Assert.DoesNotContain("missing-", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_CanValidateOwnerWorkbookWithoutEmbeddingItsPath()
    {
        var workbookPath = Environment.GetEnvironmentVariable("SSC_TEST_WORKBOOK");
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            return;
        }

        var result = CoverageWorkbookReader.Load(workbookPath);

        Assert.True(result.IsValid, JoinIssues(result));
        Assert.Equal(224, result.Dataset!.Towers.Count);
        Assert.Equal(16, result.Dataset.FibreRanges.Count);
        Assert.Collection(
            result.Warnings.OrderBy(warning => warning.RowNumber),
            warning =>
            {
                Assert.Equal(103, warning.RowNumber);
                Assert.Equal("Tower Height", warning.ColumnName);
                Assert.Equal(WorkbookValidationCode.ZeroDisablesCandidate, warning.Code);
            },
            warning =>
            {
                Assert.Equal(142, warning.RowNumber);
                Assert.Equal("Tower Range", warning.ColumnName);
                Assert.Equal(WorkbookValidationCode.ZeroDisablesCandidate, warning.Code);
            });
    }

    private static string JoinIssues(CoverageWorkbookLoadResult result)
    {
        return string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message));
    }
}
