using SimpleServiceChecker.Core.Coverage;
using SimpleServiceChecker.Core.Terrain;

namespace SimpleServiceChecker.Core.Tests;

public sealed class TerrainProfileCalculatorTests
{
    [Fact]
    public void Calculate_KnownTenKilometrePathHasExpectedMidpointFresnelAndCurvature()
    {
        var profile = Calculate(Path(10_000));
        var midpoint = profile.Samples[(profile.Samples.Count - 1) / 2];

        Assert.Equal(10_000, profile.DistanceMetres, 5);
        Assert.Equal(11.3675365, midpoint.FirstFresnelRadiusMetres, 5);
        Assert.Equal(1.471510, midpoint.CurvatureBulgeMetres, 5);
        Assert.Equal(0, profile.Samples[0].FirstFresnelRadiusMetres, 10);
        Assert.Equal(0, profile.Samples[^1].FirstFresnelRadiusMetres, 10);
    }

    [Fact]
    public void Calculate_FrequencyQuadruplingHalvesFresnelRadiusOnly()
    {
        var path = Path(10_000);
        var first = Calculate(path);
        var second = Calculate(path, Options(frequency: 23.2));
        var midpoint = (first.Samples.Count - 1) / 2;

        Assert.Equal(first.Samples[midpoint].FirstFresnelRadiusMetres / 2, second.Samples[midpoint].FirstFresnelRadiusMetres, 9);
        Assert.Equal(first.Samples[midpoint].LineOfSightMetres, second.Samples[midpoint].LineOfSightMetres, 9);
        Assert.Equal(first.Samples[midpoint].TerrainMetres, second.Samples[midpoint].TerrainMetres, 9);
    }

    [Fact]
    public void Calculate_EarthModelsApplyDistinctBulges()
    {
        var path = Path(10_000);
        var standard = Calculate(path);
        var geometric = Calculate(path, Options(model: EarthModel.Geometric));
        var flat = Calculate(path, Options(model: EarthModel.Flat));
        var midpoint = (standard.Samples.Count - 1) / 2;

        Assert.Equal(standard.Samples[midpoint].CurvatureBulgeMetres * 4 / 3, geometric.Samples[midpoint].CurvatureBulgeMetres, 8);
        Assert.Equal(0, flat.Samples[midpoint].CurvatureBulgeMetres);
        Assert.Null(flat.EffectiveEarthRadiusFactor);
    }

    [Theory]
    [InlineData(-0.001, ClearanceCategory.Obstruction)]
    [InlineData(0, ClearanceCategory.Obstruction)]
    [InlineData(3.048, ClearanceCategory.TenFeetOrLess)]
    [InlineData(9.144, ClearanceCategory.ThirtyFeetOrLess)]
    [InlineData(15.24, ClearanceCategory.FiftyFeetOrLess)]
    [InlineData(15.2401, ClearanceCategory.MoreThanFiftyFeet)]
    public void CategorizeClearance_PreservesApprovedThresholds(double clearance, ClearanceCategory expected)
    {
        Assert.Equal(expected, TerrainProfileCalculator.CategorizeClearance(clearance));
    }

    [Fact]
    public void Calculate_RidgeCanObstructLineOfSightAndFresnelReference()
    {
        var path = Path(10_000, customerHeightFeet: 100, towerHeightFeet: 100);
        var profile = Calculate(path, elevation: fraction => Math.Abs(fraction - 0.5) < 0.03 ? 250 : 200);

        Assert.False(profile.IsLineOfSightClear);
        Assert.False(profile.IsFresnelReferenceClear);
        Assert.Equal(5_000, profile.MinimumClearance.DistanceMetres, 4);
    }

    [Fact]
    public void Calculate_PositiveLineClearanceCanFailSixtyPercentFresnelReference()
    {
        var path = Path(10_000, customerHeightFeet: 25, towerHeightFeet: 25);
        var profile = Calculate(path, Options(clutterHeight: 0));

        Assert.True(profile.IsLineOfSightClear);
        Assert.False(profile.IsFresnelReferenceClear);
        Assert.True(profile.MinimumClearance.ClearanceMetres > 0);
        Assert.True(profile.MinimumFresnelReferenceMargin.FresnelReferenceMarginMetres < 0);
    }

    [Fact]
    public void Calculate_ZeroSixtyAndHundredPercentReferencesAreDistinct()
    {
        var path = Path(10_000, customerHeightFeet: 40, towerHeightFeet: 40);
        var zero = Calculate(path, Options(reference: 0, clutterHeight: 0));
        var sixty = Calculate(path, Options(reference: 60, clutterHeight: 0));
        var full = Calculate(path, Options(reference: 100, clutterHeight: 0));

        Assert.Equal(zero.MinimumClearance.ClearanceMetres, zero.MinimumFresnelReferenceMargin.FresnelReferenceMarginMetres, 9);
        Assert.True(sixty.MinimumFresnelReferenceMargin.FresnelReferenceMarginMetres > full.MinimumFresnelReferenceMargin.FresnelReferenceMarginMetres);
    }

    [Fact]
    public void Calculate_EndpointHeightsChangeTheirEndsIndependently()
    {
        var first = Calculate(Path(10_000, customerHeightFeet: 100, towerHeightFeet: 50), Options(clutterHeight: 0));
        var second = Calculate(Path(10_000, customerHeightFeet: 200, towerHeightFeet: 50), Options(clutterHeight: 0));

        Assert.Equal(30.48, first.CustomerHeightMetres, 6);
        Assert.Equal(15.24, first.TowerHeightMetres, 6);
        Assert.Equal(first.Samples[^1].LineOfSightMetres, second.Samples[^1].LineOfSightMetres, 9);
        Assert.Equal(first.Samples[0].LineOfSightMetres + 30.48, second.Samples[0].LineOfSightMetres, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(200_001)]
    public void GetSampleCount_RejectsUnsupportedPathLengths(double distanceMetres)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TerrainProfileCalculator.GetSampleCount(distanceMetres));
    }

    [Fact]
    public void GetSampleCount_IsOddBoundedAndRetainsMidpoint()
    {
        Assert.Equal(33, TerrainProfileCalculator.GetSampleCount(1.1));
        Assert.Equal(1, TerrainProfileCalculator.GetSampleCount(10_000) % 2);
        Assert.Equal(511, TerrainProfileCalculator.GetSampleCount(199_999));
    }

    [Fact]
    public void Calculate_RejectsMissingOrGeographicallyMismatchedSamples()
    {
        var path = Path(10_000);
        var samples = Samples(path).ToList();
        samples.RemoveAt(4);
        Assert.Throws<ArgumentException>(() => TerrainProfileCalculator.Calculate(path.Customer, path.Tower, samples, Options()));

        samples = Samples(path).ToList();
        samples[4] = samples[4] with
        {
            Location = samples[4].Location with { Longitude = samples[4].Location.Longitude + 0.1 },
        };
        Assert.Throws<ArgumentException>(() => TerrainProfileCalculator.Calculate(path.Customer, path.Tower, samples, Options()));
    }

    [Theory]
    [InlineData(-1, 60, 50)]
    [InlineData(5.8, -1, 50)]
    [InlineData(5.8, 101, 50)]
    [InlineData(5.8, 60, 301)]
    public void Calculate_RejectsInvalidOptions(double frequency, double reference, double clutter)
    {
        var path = Path(10_000);
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(path, Options(frequency, reference, clutterHeight: clutter)));
    }

    private static TerrainProfile Calculate(
        TestPath path,
        TerrainProfileOptions? options = null,
        Func<double, double>? elevation = null) =>
        TerrainProfileCalculator.Calculate(
            path.Customer,
            path.Tower,
            Samples(path, elevation),
            options ?? Options());

    private static IReadOnlyList<ElevationSample> Samples(TestPath path, Func<double, double>? elevation = null)
    {
        elevation ??= _ => 200;
        var distance = GeoMath.DistanceKilometres(path.Customer.Location, path.Tower.Location) * 1000;
        var count = TerrainProfileCalculator.GetSampleCount(distance);
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var fraction = index / (double)(count - 1);
                return new ElevationSample(
                    GeoMath.InterpolateGeodesic(path.Customer.Location, path.Tower.Location, fraction),
                    elevation(fraction),
                    30);
            })
            .ToArray();
    }

    private static TestPath Path(double distanceMetres, double customerHeightFeet = 100, double towerHeightFeet = 100)
    {
        var longitude = distanceMetres / (GeoMath.EarthRadiusKilometres * 1000) * 180 / Math.PI;
        return new TestPath(
            new TerrainEndpoint(new GeoCoordinate(0, 0), customerHeightFeet),
            new TerrainEndpoint(new GeoCoordinate(0, longitude), towerHeightFeet));
    }

    private static TerrainProfileOptions Options(
        double frequency = 5.8,
        double reference = 60,
        EarthModel model = EarthModel.StandardFourThirds,
        double clutterHeight = 50) =>
        new(frequency, reference, model, clutterHeight);

    private sealed record TestPath(TerrainEndpoint Customer, TerrainEndpoint Tower);
}
