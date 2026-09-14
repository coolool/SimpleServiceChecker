using SimpleServiceChecker.Core.Coverage;

namespace SimpleServiceChecker.Core.Workbooks;

public sealed record CoverageWorkbookLoadResult(
    CoverageDataset? Dataset,
    IReadOnlyList<WorkbookValidationIssue> Issues)
{
    public bool IsValid => Dataset is not null && Issues.All(issue => issue.Severity != WorkbookValidationSeverity.Error);

    public IReadOnlyList<WorkbookValidationIssue> Errors =>
        Issues.Where(issue => issue.Severity == WorkbookValidationSeverity.Error).ToArray();

    public IReadOnlyList<WorkbookValidationIssue> Warnings =>
        Issues.Where(issue => issue.Severity == WorkbookValidationSeverity.Warning).ToArray();
}
