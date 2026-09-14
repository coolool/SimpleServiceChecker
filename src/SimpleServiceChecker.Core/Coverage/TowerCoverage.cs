namespace SimpleServiceChecker.Core.Coverage;

public sealed record TowerCoverage(
    int SourceRowNumber,
    string Name,
    double HeightFeet,
    double RangeKilometres,
    double MaxDownloadMbps,
    double MaxUploadMbps,
    double? ApDirectionDegrees,
    double ApCoverageDegrees,
    GeoCoordinate Location,
    string? Notes)
{
    public bool IsOmnidirectional => ApCoverageDegrees >= 360;

    public bool CanProduceCandidate => HeightFeet > 0 && RangeKilometres > 0;
}
