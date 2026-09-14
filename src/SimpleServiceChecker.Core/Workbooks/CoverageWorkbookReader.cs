using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using SimpleServiceChecker.Core.Coverage;

namespace SimpleServiceChecker.Core.Workbooks;

public static partial class CoverageWorkbookReader
{
    public static CoverageWorkbookLoadResult Load(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using FileStream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using XLWorkbook workbook = new XLWorkbook(stream);
            return Load(workbook, Path.GetFileName(filePath), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new CoverageWorkbookLoadResult(
                null,
                [new WorkbookValidationIssue(
                    WorkbookValidationCode.FileReadFailed,
                    WorkbookValidationSeverity.Error,
                    $"The workbook could not be read ({exception.GetType().Name}).")]);
        }
    }

    public static Task<CoverageWorkbookLoadResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(filePath, cancellationToken), cancellationToken);
    }

    private static CoverageWorkbookLoadResult Load(
        XLWorkbook workbook,
        string sourceFileName,
        CancellationToken cancellationToken)
    {
        List<WorkbookValidationIssue> issues = new List<WorkbookValidationIssue>();
        var towerSheet = GetRequiredSheet(workbook, WorkbookProbe.TowerSheetName, issues);
        var fibreSheet = GetRequiredSheet(workbook, WorkbookProbe.FibreSheetName, issues);

        var towerHeaders = towerSheet is null
            ? null
            : ReadHeaders(towerSheet, WorkbookProbe.TowerHeaders, issues);
        var fibreHeaders = fibreSheet is null
            ? null
            : ReadHeaders(fibreSheet, WorkbookProbe.FibreHeaders, issues);

        if (issues.Any(issue => issue.Severity == WorkbookValidationSeverity.Error))
        {
            return new CoverageWorkbookLoadResult(null, issues);
        }

        var towers = ReadTowers(towerSheet!, towerHeaders!, issues, cancellationToken);
        var fibreRanges = ReadFibreRanges(fibreSheet!, fibreHeaders!, issues, cancellationToken);
        return issues.Any(issue => issue.Severity == WorkbookValidationSeverity.Error)
            ? new CoverageWorkbookLoadResult(null, issues)
            : new CoverageWorkbookLoadResult(
            new CoverageDataset(sourceFileName, towers, fibreRanges),
            issues);
    }

    private static IXLWorksheet? GetRequiredSheet(
        XLWorkbook workbook,
        string sheetName,
        ICollection<WorkbookValidationIssue> issues)
    {
        var normalizedMatches = workbook.Worksheets
            .Where(candidate => string.Equals(candidate.Name.Trim(), sheetName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (normalizedMatches.Length > 1)
        {
            issues.Add(new WorkbookValidationIssue(
                WorkbookValidationCode.DuplicateSheet,
                WorkbookValidationSeverity.Error,
                $"More than one worksheet resolves to the required name {sheetName}.",
                sheetName));
            return null;
        }

        var sheet = normalizedMatches.FirstOrDefault(
            candidate => string.Equals(candidate.Name, sheetName, StringComparison.Ordinal));
        if (sheet is not null)
        {
            return sheet;
        }

        issues.Add(new WorkbookValidationIssue(
            WorkbookValidationCode.MissingSheet,
            WorkbookValidationSeverity.Error,
            $"Missing required sheet: {sheetName}.",
            sheetName));
        return null;
    }

    private static IReadOnlyDictionary<string, int>? ReadHeaders(
        IXLWorksheet worksheet,
        IReadOnlyList<string> requiredHeaders,
        ICollection<WorkbookValidationIssue> issues)
    {
        var issueCount = issues.Count;
        var lastColumn = worksheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;
        Dictionary<string, int> headers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var columnNumber = 1; columnNumber <= lastColumn; columnNumber++)
        {
            var header = worksheet.Cell(1, columnNumber).GetString().Trim();
            if (header.Length == 0)
            {
                continue;
            }

            if (!headers.TryAdd(header, columnNumber))
            {
                issues.Add(new WorkbookValidationIssue(
                    WorkbookValidationCode.DuplicateHeader,
                    WorkbookValidationSeverity.Error,
                    $"{worksheet.Name} has more than one column named \"{header}\".",
                    worksheet.Name,
                    1,
                    header));
            }
        }

        foreach (var requiredHeader in requiredHeaders)
        {
            if (!headers.ContainsKey(requiredHeader))
            {
                issues.Add(new WorkbookValidationIssue(
                    WorkbookValidationCode.MissingHeader,
                    WorkbookValidationSeverity.Error,
                    $"{worksheet.Name} is missing required column \"{requiredHeader}\".",
                    worksheet.Name,
                    1,
                    requiredHeader));
            }
        }

        return issues.Count == issueCount ? headers : null;
    }

    private static IReadOnlyList<TowerCoverage> ReadTowers(
        IXLWorksheet worksheet,
        IReadOnlyDictionary<string, int> headers,
        ICollection<WorkbookValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        List<TowerCoverage> towers = new List<TowerCoverage>();
        var lastRow = worksheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsEmptyRow(worksheet, rowNumber, headers.Values))
            {
                continue;
            }

            var issueCount = issues.Count;
            var name = ReadRequiredText(worksheet, rowNumber, headers, "Tower Name", issues);
            var height = ReadRequiredDouble(worksheet, rowNumber, headers, "Tower Height", false, issues);
            var range = ReadRequiredDouble(worksheet, rowNumber, headers, "Tower Range", false, issues);
            var maxDownload = ReadRequiredDouble(worksheet, rowNumber, headers, "Max Download (Mbps)", false, issues);
            var maxUpload = ReadRequiredDouble(worksheet, rowNumber, headers, "Max Upload (Mbps)", false, issues);
            var direction = ReadOptionalDouble(worksheet, rowNumber, headers, "AP Direction (°)", true, issues);
            var coverage = ReadRequiredDouble(worksheet, rowNumber, headers, "AP Coverage (°)", true, issues);
            var latitude = ReadRequiredDouble(worksheet, rowNumber, headers, "Lat", false, issues);
            var longitude = ReadRequiredDouble(worksheet, rowNumber, headers, "lon", false, issues);
            var notes = ReadOptionalText(worksheet, rowNumber, headers, "Notes");

            ValidateMinimum(worksheet.Name, rowNumber, "Tower Height", height, 0, true, issues);
            ValidateMinimum(worksheet.Name, rowNumber, "Tower Range", range, 0, true, issues);
            ValidateMinimum(worksheet.Name, rowNumber, "Max Download (Mbps)", maxDownload, 0, false, issues);
            ValidateMinimum(worksheet.Name, rowNumber, "Max Upload (Mbps)", maxUpload, 0, false, issues);
            ValidateRange(worksheet.Name, rowNumber, "AP Coverage (°)", coverage, 0, 360, false, true, issues);
            ValidateRange(worksheet.Name, rowNumber, "Lat", latitude, -90, 90, true, true, issues);
            ValidateRange(worksheet.Name, rowNumber, "lon", longitude, -180, 180, true, true, issues);

            if (direction is not null)
            {
                ValidateRange(worksheet.Name, rowNumber, "AP Direction (°)", direction, 0, 360, true, true, issues);
            }

            if (coverage is > 0 and < 360 && direction is null)
            {
                issues.Add(new WorkbookValidationIssue(
                    WorkbookValidationCode.DirectionRequired,
                    WorkbookValidationSeverity.Error,
                    $"{worksheet.Name} row {rowNumber}, AP Direction (°): enter a direction when AP Coverage (°) is less than 360.",
                    worksheet.Name,
                    rowNumber,
                    "AP Direction (°)"));
            }

            if (issues.Count != issueCount ||
                name is null ||
                height is null ||
                range is null ||
                maxDownload is null ||
                maxUpload is null ||
                coverage is null ||
                latitude is null ||
                longitude is null)
            {
                continue;
            }

            if (height == 0)
            {
                AddZeroCandidateWarning(worksheet.Name, rowNumber, "Tower Height", issues);
            }

            if (range == 0)
            {
                AddZeroCandidateWarning(worksheet.Name, rowNumber, "Tower Range", issues);
            }

            towers.Add(new TowerCoverage(
                rowNumber,
                name,
                height.Value,
                range.Value,
                maxDownload.Value,
                maxUpload.Value,
                direction is null ? null : GeoMath.NormalizeDegrees(direction.Value),
                coverage.Value,
                new GeoCoordinate(latitude.Value, longitude.Value),
                notes));
        }

        return towers;
    }

    private static IReadOnlyList<FibreCoverageRange> ReadFibreRanges(
        IXLWorksheet worksheet,
        IReadOnlyDictionary<string, int> headers,
        ICollection<WorkbookValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        List<FibreCoverageRange> ranges = new List<FibreCoverageRange>();
        var lastRow = worksheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsEmptyRow(worksheet, rowNumber, headers.Values))
            {
                continue;
            }

            var issueCount = issues.Count;
            var start = ReadRequiredInteger(worksheet, rowNumber, headers, "Start_Civic_Num", issues);
            var end = ReadEndCivicNumber(worksheet, rowNumber, headers, issues);
            var streetName = ReadRequiredText(worksheet, rowNumber, headers, "Street_Name", issues);
            var streetType = ReadRequiredText(worksheet, rowNumber, headers, "Street_Type", issues);
            var city = ReadRequiredText(worksheet, rowNumber, headers, "City", issues);
            var postalCode = ReadOptionalText(worksheet, rowNumber, headers, "Postal_Code");
            var maxSpeeds = ReadRequiredText(worksheet, rowNumber, headers, "Max_Speeds", issues);
            var notes = ReadOptionalText(worksheet, rowNumber, headers, "Notes");

            if (start is < 0)
            {
                AddRangeError(worksheet.Name, rowNumber, "Start_Civic_Num", "use a whole number of zero or greater.", issues);
            }

            if (start is not null && end.Value.HasValue && end.Value.Value < start.Value)
            {
                issues.Add(new WorkbookValidationIssue(
                    WorkbookValidationCode.InvalidCivicRange,
                    WorkbookValidationSeverity.Error,
                    $"{worksheet.Name} row {rowNumber}: End_Civic_Num must be greater than or equal to Start_Civic_Num.",
                    worksheet.Name,
                    rowNumber,
                    "End_Civic_Num"));
            }

            if (maxSpeeds is not null && !SpeedFormat().IsMatch(maxSpeeds))
            {
                issues.Add(new WorkbookValidationIssue(
                    WorkbookValidationCode.InvalidSpeedFormat,
                    WorkbookValidationSeverity.Error,
                    $"{worksheet.Name} row {rowNumber}, Max_Speeds: use a speed such as 150/150 or 1000 Mbps.",
                    worksheet.Name,
                    rowNumber,
                    "Max_Speeds"));
            }

            if (issues.Count != issueCount ||
                start is null ||
                !end.IsValid ||
                streetName is null ||
                streetType is null ||
                city is null ||
                maxSpeeds is null)
            {
                continue;
            }

            ranges.Add(new FibreCoverageRange(
                rowNumber,
                start.Value,
                end.Value,
                streetName,
                streetType,
                city,
                postalCode,
                maxSpeeds,
                notes));
        }

        return ranges;
    }

    private static bool IsEmptyRow(IXLWorksheet worksheet, int rowNumber, IEnumerable<int> columnNumbers)
    {
        return columnNumbers.All(columnNumber => worksheet.Cell(rowNumber, columnNumber).IsEmpty());
    }

    private static string? ReadRequiredText(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string header,
        ICollection<WorkbookValidationIssue> issues)
    {
        var value = ReadOptionalText(worksheet, rowNumber, headers, header);
        if (value is not null)
        {
            return value;
        }

        issues.Add(new WorkbookValidationIssue(
            WorkbookValidationCode.RequiredValueMissing,
            WorkbookValidationSeverity.Error,
            $"{worksheet.Name} row {rowNumber}, {header}: enter a value.",
            worksheet.Name,
            rowNumber,
            header));
        return null;
    }

    private static string? ReadOptionalText(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string header)
    {
        var value = worksheet.Cell(rowNumber, headers[header]).GetString().Trim();
        return value.Length == 0 ? null : value;
    }

    private static double? ReadRequiredDouble(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string header,
        bool allowDegreeSymbol,
        ICollection<WorkbookValidationIssue> issues)
    {
        var cell = worksheet.Cell(rowNumber, headers[header]);
        if (cell.IsEmpty())
        {
            issues.Add(new WorkbookValidationIssue(
                WorkbookValidationCode.RequiredValueMissing,
                WorkbookValidationSeverity.Error,
                $"{worksheet.Name} row {rowNumber}, {header}: enter a number.",
                worksheet.Name,
                rowNumber,
                header));
            return null;
        }

        if (TryReadDouble(cell, allowDegreeSymbol, out var value))
        {
            return value;
        }

        issues.Add(new WorkbookValidationIssue(
            WorkbookValidationCode.InvalidNumber,
            WorkbookValidationSeverity.Error,
            $"{worksheet.Name} row {rowNumber}, {header}: \"{SafeDisplay(cell)}\" is not a valid number.",
            worksheet.Name,
            rowNumber,
            header));
        return null;
    }

    private static double? ReadOptionalDouble(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string header,
        bool allowDegreeSymbol,
        ICollection<WorkbookValidationIssue> issues)
    {
        var cell = worksheet.Cell(rowNumber, headers[header]);
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString()))
        {
            return null;
        }

        if (TryReadDouble(cell, allowDegreeSymbol, out var value))
        {
            return value;
        }

        issues.Add(new WorkbookValidationIssue(
            WorkbookValidationCode.InvalidNumber,
            WorkbookValidationSeverity.Error,
            $"{worksheet.Name} row {rowNumber}, {header}: \"{SafeDisplay(cell)}\" is not a valid number.",
            worksheet.Name,
            rowNumber,
            header));
        return null;
    }

    private static int? ReadRequiredInteger(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        string header,
        ICollection<WorkbookValidationIssue> issues)
    {
        var value = ReadRequiredDouble(worksheet, rowNumber, headers, header, false, issues);
        if (value is null)
        {
            return null;
        }

        if (value >= int.MinValue && value <= int.MaxValue && Math.Abs(value.Value - Math.Round(value.Value)) < 1e-9)
        {
            return checked((int)Math.Round(value.Value));
        }

        AddRangeError(worksheet.Name, rowNumber, header, "use a whole number.", issues);
        return null;
    }

    private static EndCivicReadResult ReadEndCivicNumber(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> headers,
        ICollection<WorkbookValidationIssue> issues)
    {
        var cell = worksheet.Cell(rowNumber, headers["End_Civic_Num"]);
        var raw = cell.GetString().Trim();
        if (raw == "*")
        {
            return new EndCivicReadResult(true, null);
        }

        var value = ReadRequiredInteger(worksheet, rowNumber, headers, "End_Civic_Num", issues);
        if (value is null)
        {
            return new EndCivicReadResult(false, null);
        }

        if (value < 0)
        {
            AddRangeError(worksheet.Name, rowNumber, "End_Civic_Num", "use a whole number of zero or greater, or *.", issues);
            return new EndCivicReadResult(false, null);
        }

        return new EndCivicReadResult(true, value);
    }

    private static bool TryReadDouble(IXLCell cell, bool allowDegreeSymbol, out double value)
    {
        if (cell.DataType == XLDataType.Number)
        {
            value = cell.GetDouble();
            return double.IsFinite(value);
        }

        var text = cell.GetString().Trim();
        if (allowDegreeSymbol && text.EndsWith('°'))
        {
            text = text[..^1].TrimEnd();
        }

        return double.TryParse(
            text,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value) && double.IsFinite(value);
    }

    private static void ValidateMinimum(
        string sheetName,
        int rowNumber,
        string columnName,
        double? value,
        double minimum,
        bool allowEqual,
        ICollection<WorkbookValidationIssue> issues)
    {
        if (value is null || (allowEqual ? value >= minimum : value > minimum))
        {
            return;
        }

        AddRangeError(
            sheetName,
            rowNumber,
            columnName,
            allowEqual ? $"use a number of {minimum} or greater." : $"use a number greater than {minimum}.",
            issues);
    }

    private static void ValidateRange(
        string sheetName,
        int rowNumber,
        string columnName,
        double? value,
        double minimum,
        double maximum,
        bool includeMinimum,
        bool includeMaximum,
        ICollection<WorkbookValidationIssue> issues)
    {
        if (value is null)
        {
            return;
        }

        var validMinimum = includeMinimum ? value >= minimum : value > minimum;
        var validMaximum = includeMaximum ? value <= maximum : value < maximum;
        if (validMinimum && validMaximum)
        {
            return;
        }

        var minimumText = includeMinimum ? $"{minimum} or greater" : $"greater than {minimum}";
        var maximumText = includeMaximum ? $"{maximum} or less" : $"less than {maximum}";
        AddRangeError(sheetName, rowNumber, columnName, $"use a number {minimumText} and {maximumText}.", issues);
    }

    private static void AddRangeError(
        string sheetName,
        int rowNumber,
        string columnName,
        string guidance,
        ICollection<WorkbookValidationIssue> issues)
    {
        issues.Add(new WorkbookValidationIssue(
            WorkbookValidationCode.NumberOutOfRange,
            WorkbookValidationSeverity.Error,
            $"{sheetName} row {rowNumber}, {columnName}: {guidance}",
            sheetName,
            rowNumber,
            columnName));
    }

    private static void AddZeroCandidateWarning(
        string sheetName,
        int rowNumber,
        string columnName,
        ICollection<WorkbookValidationIssue> issues)
    {
        issues.Add(new WorkbookValidationIssue(
            WorkbookValidationCode.ZeroDisablesCandidate,
            WorkbookValidationSeverity.Warning,
            $"{sheetName} row {rowNumber}, {columnName}: zero prevents this tower from being returned as a normal wireless candidate.",
            sheetName,
            rowNumber,
            columnName));
    }

    private static string SafeDisplay(IXLCell cell)
    {
        var value = cell.GetString().Trim();
        return value.Length <= 40 ? value : $"{value[..37]}...";
    }

    [GeneratedRegex(@"^\d+(?:\.\d+)?(?:\s*/\s*\d+(?:\.\d+)?)?\s*(?:Mbps|Gbps)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpeedFormat();

    private readonly record struct EndCivicReadResult(bool IsValid, int? Value);
}
