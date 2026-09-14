using System.Text;

namespace SimpleServiceChecker.Core.Coverage;

public sealed record FibreAddress(
    int? CivicNumber,
    string? StreetName,
    string? StreetType,
    string? City,
    string? PostalCode);

public enum FibreAvailabilityStatus
{
    Available,
    NotListed,
    AddressRequired,
    DataError,
}

public sealed record FibreMatchResult(
    FibreAvailabilityStatus Status,
    FibreCoverageRange? Match,
    IReadOnlyList<string> Warnings)
{
    public static FibreMatchResult AddressRequired()
    {
        return new(FibreAvailabilityStatus.AddressRequired, null, []);
    }

    public static FibreMatchResult NotListed()
    {
        return new(FibreAvailabilityStatus.NotListed, null, []);
    }
}

public static class FibreMatcher
{
    public static FibreMatchResult Match(
        IEnumerable<FibreCoverageRange> ranges,
        FibreAddress address)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(address);

        if (address.CivicNumber is null ||
            address.CivicNumber < 0 ||
            string.IsNullOrWhiteSpace(address.StreetName) ||
            string.IsNullOrWhiteSpace(address.StreetType) ||
            string.IsNullOrWhiteSpace(address.City))
        {
            return FibreMatchResult.AddressRequired();
        }

        var streetName = AddressNormalizer.NormalizeWords(address.StreetName);
        var streetType = AddressNormalizer.NormalizeStreetType(address.StreetType);
        var city = AddressNormalizer.NormalizeWords(address.City);
        var postalCode = AddressNormalizer.NormalizePostalCode(address.PostalCode);

        var matches = ranges
            .Where(range => range.Contains(address.CivicNumber.Value))
            .Where(range => AddressNormalizer.NormalizeWords(range.StreetName) == streetName)
            .Where(range => AddressNormalizer.NormalizeStreetType(range.StreetType) == streetType)
            .Where(range => AddressNormalizer.NormalizeWords(range.City) == city)
            .Where(range => PostalCodesMatch(range.PostalCode, postalCode))
            .OrderBy(range => range.RangeWidth)
            .ThenBy(range => range.SourceRowNumber)
            .ToArray();

        if (matches.Length == 0)
        {
            return FibreMatchResult.NotListed();
        }

        var selected = matches[0];
        var hasConflict = matches
            .Skip(1)
            .Any(match =>
                !string.Equals(match.MaxSpeeds, selected.MaxSpeeds, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(match.Notes, selected.Notes, StringComparison.OrdinalIgnoreCase));
        IReadOnlyList<string> warnings = hasConflict
            ? ["More than one fibre row matches this address with conflicting service details. The most specific civic range is shown."]
            : [];

        return new FibreMatchResult(FibreAvailabilityStatus.Available, selected, warnings);
    }

    private static bool PostalCodesMatch(string? rangePostalCode, string addressPostalCode)
    {
        var normalizedRangePostalCode = AddressNormalizer.NormalizePostalCode(rangePostalCode);
        return normalizedRangePostalCode.Length == 0 ||
            addressPostalCode.Length == 0 ||
            normalizedRangePostalCode == addressPostalCode;
    }
}

public static class AddressNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> StreetTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AV"] = "AVE",
            ["AVE"] = "AVE",
            ["AVENUE"] = "AVE",
            ["BLVD"] = "BLVD",
            ["BOULEVARD"] = "BLVD",
            ["CIR"] = "CIR",
            ["CIRCLE"] = "CIR",
            ["COURT"] = "CT",
            ["CRES"] = "CRES",
            ["CRESCENT"] = "CRES",
            ["CT"] = "CT",
            ["DR"] = "DR",
            ["DRIVE"] = "DR",
            ["HIGHWAY"] = "HWY",
            ["HWY"] = "HWY",
            ["LANE"] = "LN",
            ["LINE"] = "LINE",
            ["LN"] = "LN",
            ["PL"] = "PL",
            ["PLACE"] = "PL",
            ["RD"] = "RD",
            ["ROAD"] = "RD",
            ["ROUTE"] = "RTE",
            ["RTE"] = "RTE",
            ["SIDEROAD"] = "SRD",
            ["SRD"] = "SRD",
            ["ST"] = "ST",
            ["STREET"] = "ST",
            ["TER"] = "TER",
            ["TERRACE"] = "TER",
            ["TRAIL"] = "TRL",
            ["TRL"] = "TRL",
            ["WAY"] = "WAY",
        };

    public static string NormalizeWords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().Normalize(NormalizationForm.FormKC))
        {
            if (char.IsLetterOrDigit(character))
            {
                _ = builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    public static string NormalizeStreetType(string? value)
    {
        var normalized = NormalizeWords(value);
        return StreetTypes.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    public static bool TrySplitStreetRoute(string? route, out string streetName, out string streetType)
    {
        streetName = string.Empty;
        streetType = string.Empty;
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        var parts = route
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        for (var index = parts.Count - 1; index >= 1; index--)
        {
            var normalizedPart = NormalizeWords(parts[index]);
            if (!StreetTypes.TryGetValue(normalizedPart, out var canonicalType))
            {
                continue;
            }

            streetType = canonicalType;
            parts.RemoveAt(index);
            streetName = string.Join(' ', parts);
            return streetName.Length > 0;
        }

        return false;
    }

    public static string NormalizePostalCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }
}
