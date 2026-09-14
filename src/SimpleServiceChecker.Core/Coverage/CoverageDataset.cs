namespace SimpleServiceChecker.Core.Coverage;

public sealed record CoverageDataset(
    string SourceFileName,
    IReadOnlyList<TowerCoverage> Towers,
    IReadOnlyList<FibreCoverageRange> FibreRanges);
