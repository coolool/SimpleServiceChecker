namespace SimpleServiceChecker.Core.Workbooks;

public enum WorkbookValidationSeverity
{
    Warning,
    Error,
}

public enum WorkbookValidationCode
{
    FileReadFailed,
    MissingSheet,
    DuplicateSheet,
    MissingHeader,
    DuplicateHeader,
    RequiredValueMissing,
    InvalidNumber,
    NumberOutOfRange,
    DirectionRequired,
    InvalidCivicRange,
    InvalidSpeedFormat,
    ZeroDisablesCandidate,
}

public sealed record WorkbookValidationIssue(
    WorkbookValidationCode Code,
    WorkbookValidationSeverity Severity,
    string Message,
    string? SheetName = null,
    int? RowNumber = null,
    string? ColumnName = null);
