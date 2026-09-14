using ClosedXML.Excel;
using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.Core.Tests;

public sealed class WorkbookProbeTests
{
    [Fact]
    public void Probe_AcceptsTheRequiredTwoSheetContract()
    {
        var path = CreateWorkbook();
        try
        {
            var result = WorkbookProbe.Probe(path);

            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
            Assert.Collection(
                result.Sheets,
                tower =>
                {
                    Assert.Equal(WorkbookProbe.TowerSheetName, tower.Name);
                    Assert.Equal(2, tower.DataRowCount);
                    Assert.Equal(10, tower.ColumnCount);
                    Assert.Empty(tower.MissingHeaders);
                },
                fibre =>
                {
                    Assert.Equal(WorkbookProbe.FibreSheetName, fibre.Name);
                    Assert.Equal(1, fibre.DataRowCount);
                    Assert.Equal(8, fibre.ColumnCount);
                    Assert.Empty(fibre.MissingHeaders);
                });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Probe_ReportsMissingRequiredHeaders()
    {
        var path = CreateWorkbook(omitFibreHeader: "City");
        try
        {
            var result = WorkbookProbe.Probe(path);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("City", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Probe_ReturnsAValidationErrorForAMalformedWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ssc-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(path, "This is not an Excel workbook.");
        try
        {
            var result = WorkbookProbe.Probe(path);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.StartsWith("The workbook could not be read", result.Errors[0], StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Probe_CanValidateTheOwnerWorkbookWithoutEmbeddingItsPath()
    {
        var workbookPath = Environment.GetEnvironmentVariable("SSC_TEST_WORKBOOK");
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            return;
        }

        var result = WorkbookProbe.Probe(workbookPath);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(224, result.Sheets.Single(sheet => sheet.Name == WorkbookProbe.TowerSheetName).DataRowCount);
        Assert.Equal(16, result.Sheets.Single(sheet => sheet.Name == WorkbookProbe.FibreSheetName).DataRowCount);
    }

    private static string CreateWorkbook(string? omitFibreHeader = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ssc-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        AddSheet(workbook, WorkbookProbe.TowerSheetName, WorkbookProbe.TowerHeaders, 2);
        AddSheet(
            workbook,
            WorkbookProbe.FibreSheetName,
            WorkbookProbe.FibreHeaders.Where(header => header != omitFibreHeader).ToArray(),
            1);
        workbook.SaveAs(path);
        return path;
    }

    private static void AddSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers, int rowCount)
    {
        var sheet = workbook.AddWorksheet(name);
        for (var column = 0; column < headers.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < headers.Count; column++)
            {
                sheet.Cell(row + 2, column + 1).Value = $"Value {row + 1}-{column + 1}";
            }
        }
    }
}
