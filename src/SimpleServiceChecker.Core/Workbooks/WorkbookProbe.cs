using ClosedXML.Excel;

namespace SimpleServiceChecker.Core.Workbooks;

public static class WorkbookProbe
{
    public const string TowerSheetName = "Tower Coverage";
    public const string FibreSheetName = "Fibre Coverage";

    public static readonly IReadOnlyList<string> TowerHeaders =
    [
        "Tower Name",
        "Tower Height",
        "Tower Range",
        "Max Download (Mbps)",
        "Max Upload (Mbps)",
        "AP Direction (°)",
        "AP Coverage (°)",
        "Lat",
        "lon",
        "Notes",
    ];

    public static readonly IReadOnlyList<string> FibreHeaders =
    [
        "Start_Civic_Num",
        "End_Civic_Num",
        "Street_Name",
        "Street_Type",
        "City",
        "Postal_Code",
        "Max_Speeds",
        "Notes",
    ];

    public static WorkbookProbeResult Probe(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var workbook = new XLWorkbook(stream);
            var sheets = new List<WorksheetProbeResult>();
            var errors = new List<string>();

            ProbeSheet(workbook, TowerSheetName, TowerHeaders, sheets, errors);
            ProbeSheet(workbook, FibreSheetName, FibreHeaders, sheets, errors);

            return new WorkbookProbeResult(errors.Count == 0, sheets, errors);
        }
        catch (Exception exception)
        {
            return new WorkbookProbeResult(false, [], [$"The workbook could not be read ({exception.GetType().Name})."]);
        }
    }

    private static void ProbeSheet(
        XLWorkbook workbook,
        string sheetName,
        IReadOnlyList<string> requiredHeaders,
        ICollection<WorksheetProbeResult> sheets,
        ICollection<string> errors)
    {
        if (!workbook.TryGetWorksheet(sheetName, out var worksheet))
        {
            errors.Add($"Missing required sheet: {sheetName}.");
            return;
        }

        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        string[] actualHeaders = lastColumn == 0
            ? []
            : worksheet.Row(1).Cells(1, lastColumn).Select(cell => cell.GetString().Trim()).ToArray();
        var missingHeaders = requiredHeaders.Except(actualHeaders, StringComparer.Ordinal).ToArray();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var dataRowCount = Math.Max(0, lastRow - 1);

        if (missingHeaders.Length > 0)
        {
            errors.Add($"{sheetName} is missing required column(s): {string.Join(", ", missingHeaders)}.");
        }

        sheets.Add(new WorksheetProbeResult(sheetName, dataRowCount, lastColumn, missingHeaders));
    }
}

public sealed record WorkbookProbeResult(
    bool IsValid,
    IReadOnlyList<WorksheetProbeResult> Sheets,
    IReadOnlyList<string> Errors);

public sealed record WorksheetProbeResult(
    string Name,
    int DataRowCount,
    int ColumnCount,
    IReadOnlyList<string> MissingHeaders);
