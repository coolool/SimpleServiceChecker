namespace SimpleServiceChecker.Core.Coverage;

public sealed record CoverageWorkbookManifest(
    int SchemaVersion,
    string SourceFileName,
    string Sha256Hex,
    DateTimeOffset ActivatedAtUtc,
    int TowerCount,
    int FibreRangeCount,
    int WarningCount,
    IReadOnlyList<string>? Warnings = null)
{
    public const int CurrentSchemaVersion = 1;
}
