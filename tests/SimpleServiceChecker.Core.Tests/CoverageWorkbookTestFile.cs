using ClosedXML.Excel;
using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.Core.Tests;

internal sealed class CoverageWorkbookTestFile : IDisposable
{
    private CoverageWorkbookTestFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static CoverageWorkbookTestFile Create(
        IReadOnlyList<string>? towerHeaders = null,
        IReadOnlyList<string>? fibreHeaders = null,
        Action<XLWorkbook>? configure = null)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ssc-{Guid.NewGuid():N}.xlsx");
        using XLWorkbook workbook = new XLWorkbook();
        var towerSheet = workbook.AddWorksheet(WorkbookProbe.TowerSheetName);
        AddHeaders(towerSheet, towerHeaders ?? WorkbookProbe.TowerHeaders);
        SetTowerRow(towerSheet, 2);

        var fibreSheet = workbook.AddWorksheet(WorkbookProbe.FibreSheetName);
        AddHeaders(fibreSheet, fibreHeaders ?? WorkbookProbe.FibreHeaders);
        SetFibreRow(fibreSheet, 2);

        configure?.Invoke(workbook);
        workbook.SaveAs(path);
        return new CoverageWorkbookTestFile(path);
    }

    public static void SetTowerRow(IXLWorksheet sheet, int rowNumber)
    {
        Set(sheet, rowNumber, "Tower Name", "Test Tower");
        Set(sheet, rowNumber, "Tower Height", 100d);
        Set(sheet, rowNumber, "Tower Range", 25d);
        Set(sheet, rowNumber, "Max Download (Mbps)", 100d);
        Set(sheet, rowNumber, "Max Upload (Mbps)", 20d);
        Set(sheet, rowNumber, "AP Direction (°)", null);
        Set(sheet, rowNumber, "AP Coverage (°)", 360d);
        Set(sheet, rowNumber, "Lat", 46.5d);
        Set(sheet, rowNumber, "lon", -84.3d);
        Set(sheet, rowNumber, "Notes", null);
    }

    public static void SetFibreRow(IXLWorksheet sheet, int rowNumber)
    {
        Set(sheet, rowNumber, "Start_Civic_Num", 100d);
        Set(sheet, rowNumber, "End_Civic_Num", 200d);
        Set(sheet, rowNumber, "Street_Name", "Main");
        Set(sheet, rowNumber, "Street_Type", "Rd");
        Set(sheet, rowNumber, "City", "Sault Ste. Marie");
        Set(sheet, rowNumber, "Postal_Code", "P6A 1A1");
        Set(sheet, rowNumber, "Max_Speeds", "150/150");
        Set(sheet, rowNumber, "Notes", null);
    }

    public static void Set(IXLWorksheet sheet, int rowNumber, string header, object? value)
    {
        var headerCell = sheet.Row(1).CellsUsed().FirstOrDefault(
            cell => string.Equals(cell.GetString().Trim(), header, StringComparison.Ordinal));
        if (headerCell is null)
        {
            return;
        }

        var cell = sheet.Cell(rowNumber, headerCell.Address.ColumnNumber);
        if (value is null)
        {
            _ = cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = XLCellValue.FromObject(value);
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
}
