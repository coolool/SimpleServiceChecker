namespace SimpleServiceChecker.Core.Coverage;

public sealed record CoverageWorkbookManifest(
    int SchemaVersion,
    string SourceFileName,
    string Sha256Hex,
    DateTimeOffset ActivatedAtUtc,
    int TowerCount,
    int FibreRangeCount,
    int WarningCount)
{
    public const int CurrentSchemaVersion = 1;
}
