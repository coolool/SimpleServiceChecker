using SimpleServiceChecker.Core.Coverage;

namespace SimpleServiceChecker.Core.Tests;

public sealed class FibreMatcherTests
{
    [Fact]
    public void Match_NormalizesCasePunctuationSpacingAndStreetType()
    {
        var ranges = new[] { Fibre(100, 200, "St. Joseph's", "Road", "Sault Ste Marie", "P6A 1A1") };
        FibreAddress address = new FibreAddress(150, "  st josephs ", "RD.", "SAULT  STE. MARIE", "p6a1a1");

        var result = FibreMatcher.Match(ranges, address);

        Assert.Equal(FibreAvailabilityStatus.Available, result.Status);
        Assert.Equal(2, result.Match!.SourceRowNumber);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(200, true)]
    [InlineData(99, false)]
    [InlineData(201, false)]
    public void Match_UsesInclusiveFiniteRangeBoundaries(int civicNumber, bool expected)
    {
        var result = FibreMatcher.Match(
            [Fibre(100, 200)],
            Address(civicNumber));

        Assert.Equal(expected ? FibreAvailabilityStatus.Available : FibreAvailabilityStatus.NotListed, result.Status);
    }

    [Fact]
    public void Match_OpenEndedRangeHasNoUpperLimit()
    {
        var result = FibreMatcher.Match([Fibre(100, null)], Address(100000));

        Assert.Equal(FibreAvailabilityStatus.Available, result.Status);
    }

    [Fact]
    public void Match_UsesPostalCodeAsADiscriminatorWhenBothArePresent()
    {
        var range = Fibre(100, 200, postalCode: "P6A 1A1");

        var wrongPostalCode = FibreMatcher.Match([range], Address(150) with { PostalCode = "P6A 2B2" });
        var unavailablePostalCode = FibreMatcher.Match([range], Address(150) with { PostalCode = null });

        Assert.Equal(FibreAvailabilityStatus.NotListed, wrongPostalCode.Status);
        Assert.Equal(FibreAvailabilityStatus.Available, unavailablePostalCode.Status);
    }

    [Fact]
    public void Match_SelectsMostSpecificRangeAndWarnsOnConflictingDetails()
    {
        var broad = Fibre(1, 500) with { SourceRowNumber = 2, MaxSpeeds = "150/150" };
        var narrow = Fibre(100, 200) with { SourceRowNumber = 3, MaxSpeeds = "1000/1000" };

        var result = FibreMatcher.Match([broad, narrow], Address(150));

        Assert.Equal(narrow, result.Match);
        _ = Assert.Single(result.Warnings);
    }

    [Fact]
    public void Match_RequiresStructuredCivicStreetAndCity()
    {
        var address = Address(150) with { StreetName = null };

        var result = FibreMatcher.Match([Fibre(100, 200)], address);

        Assert.Equal(FibreAvailabilityStatus.AddressRequired, result.Status);
    }

    [Theory]
    [InlineData("Great Northern Road", "Great Northern", "RD")]
    [InlineData("Queen Street East", "Queen East", "ST")]
    [InlineData("Government Rd.", "Government", "RD")]
    public void TrySplitStreetRoute_ExtractsGoogleRouteStreetType(
        string route,
        string expectedName,
        string expectedType)
    {
        var success = AddressNormalizer.TrySplitStreetRoute(route, out var streetName, out var streetType);

        Assert.True(success);
        Assert.Equal(expectedName, streetName);
        Assert.Equal(expectedType, streetType);
    }

    private static FibreCoverageRange Fibre(
        int start,
        int? end,
        string streetName = "Main",
        string streetType = "Rd",
        string city = "Sault Ste Marie",
        string? postalCode = "P6A 1A1")
    {
        return new(2, start, end, streetName, streetType, city, postalCode, "150/150", null);
    }

    private static FibreAddress Address(int civicNumber)
    {
        return new(civicNumber, "Main", "Road", "Sault Ste Marie", "P6A1A1");
    }
}
