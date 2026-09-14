namespace SimpleServiceChecker.Core.Coverage;

public sealed record FibreCoverageRange(
    int SourceRowNumber,
    int StartCivicNumber,
    int? EndCivicNumber,
    string StreetName,
    string StreetType,
    string City,
    string? PostalCode,
    string MaxSpeeds,
    string? Notes)
{
    public bool Contains(int civicNumber) =>
        civicNumber >= StartCivicNumber &&
        (EndCivicNumber is null || civicNumber <= EndCivicNumber.Value);

    public long RangeWidth => EndCivicNumber is null
        ? long.MaxValue
        : (long)EndCivicNumber.Value - StartCivicNumber;
}
