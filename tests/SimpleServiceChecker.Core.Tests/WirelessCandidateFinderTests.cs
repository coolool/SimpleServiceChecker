using SimpleServiceChecker.Core.Coverage;

namespace SimpleServiceChecker.Core.Tests;

public sealed class WirelessCandidateFinderTests
{
    [Fact]
    public void DistanceKilometres_MatchesKnownOneDegreeEquatorDistance()
    {
        var distance = GeoMath.DistanceKilometres(new GeoCoordinate(0, 0), new GeoCoordinate(0, 1));

        Assert.InRange(distance, 111.19, 111.20);
    }

    [Fact]
    public void Find_IncludesTowerExactlyAtPublishedRangeBoundary()
    {
        var customer = new GeoCoordinate(46.5, -84.3);
        var towerLocation = new GeoCoordinate(46.6, -84.3);
        var exactDistance = GeoMath.DistanceKilometres(customer, towerLocation);
        var tower = Tower("Boundary", towerLocation, exactDistance);

        var candidates = WirelessCandidateFinder.Find([tower], customer);

        Assert.Single(candidates);
        Assert.Equal(exactDistance, candidates[0].DistanceKilometres, 9);
    }

    [Fact]
    public void Find_FiltersRangeAndOrdersNearestFirst()
    {
        var customer = new GeoCoordinate(0, 0);
        var far = Tower("Far", new GeoCoordinate(0, 0.2), 100);
        var near = Tower("Near", new GeoCoordinate(0, 0.1), 100);
        var outside = Tower("Outside", new GeoCoordinate(0, 1), 10);

        var candidates = WirelessCandidateFinder.Find([far, outside, near], customer);

        Assert.Collection(candidates, candidate => Assert.Equal("Near", candidate.Tower.Name), candidate => Assert.Equal("Far", candidate.Tower.Name));
    }

    [Fact]
    public void Find_ExcludesZeroHeightOrRangeRows()
    {
        var customer = new GeoCoordinate(0, 0);
        var zeroHeight = Tower("Zero height", customer, 10) with { HeightFeet = 0 };
        var zeroRange = Tower("Zero range", customer, 0);

        var candidates = WirelessCandidateFinder.Find([zeroHeight, zeroRange], customer);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Find_AppliesFutureSafeSectorFiltering()
    {
        var towerLocation = new GeoCoordinate(0, 0);
        var eastCustomer = new GeoCoordinate(0, 0.1);
        var eastSector = Tower("East sector", towerLocation, 100) with
        {
            ApDirectionDegrees = 90,
            ApCoverageDegrees = 60,
        };
        var westSector = eastSector with
        {
            Name = "West sector",
            ApDirectionDegrees = 270,
        };

        var candidates = WirelessCandidateFinder.Find([westSector, eastSector], eastCustomer);

        var candidate = Assert.Single(candidates);
        Assert.Equal("East sector", candidate.Tower.Name);
    }

    [Theory]
    [InlineData(350, 20, 0, true)]
    [InlineData(10, 20, 0, true)]
    [InlineData(11, 20, 0, false)]
    [InlineData(180, 360, 0, true)]
    public void SectorMath_HandlesNorthWrapAndInclusiveBoundaries(
        double bearing,
        double coverage,
        double direction,
        bool expected)
    {
        Assert.Equal(expected, SectorMath.ContainsBearing(direction, coverage, bearing));
    }

    private static TowerCoverage Tower(string name, GeoCoordinate location, double rangeKilometres) =>
        new(2, name, 100, rangeKilometres, 100, 20, null, 360, location, null);
}
