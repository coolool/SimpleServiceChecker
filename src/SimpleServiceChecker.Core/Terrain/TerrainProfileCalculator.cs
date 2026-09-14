using SimpleServiceChecker.Core.Coverage;

namespace SimpleServiceChecker.Core.Terrain;

public enum EarthModel
{
    StandardFourThirds,
    Geometric,
    Flat,
}

public enum ClearanceCategory
{
    Obstruction,
    TenFeetOrLess,
    ThirtyFeetOrLess,
    FiftyFeetOrLess,
    MoreThanFiftyFeet,
}

public sealed record TerrainEndpoint(GeoCoordinate Location, double HeightFeet);

public sealed record ElevationSample(
    GeoCoordinate Location,
    double ElevationMetres,
    double? ResolutionMetres);

public sealed record TerrainProfileOptions(
    double FrequencyGigahertz,
    double FresnelClearancePercent,
    EarthModel EarthModel,
    double ClutterHeightFeet);

public sealed record TerrainProfileSample(
    int Index,
    GeoCoordinate Location,
    double DistanceMetres,
    double RawElevationMetres,
    double CurvatureBulgeMetres,
    double TerrainMetres,
    double ClutterTopMetres,
    double LineOfSightMetres,
    double FirstFresnelRadiusMetres,
    double FresnelReferenceRadiusMetres,
    double ClearanceMetres,
    double FresnelReferenceMarginMetres,
    ClearanceCategory ClearanceCategory);

public sealed record TerrainProfile(
    double DistanceMetres,
    double BearingDegrees,
    double SampleSpacingMetres,
    double FrequencyGigahertz,
    double FresnelClearancePercent,
    EarthModel EarthModel,
    double? EffectiveEarthRadiusFactor,
    double CustomerHeightMetres,
    double TowerHeightMetres,
    double ClutterHeightMetres,
    double? MaximumResolutionMetres,
    int UnknownResolutionCount,
    bool IsLineOfSightClear,
    bool IsFresnelReferenceClear,
    TerrainProfileSample MinimumClearance,
    TerrainProfileSample MinimumFresnelReferenceMargin,
    IReadOnlyList<TerrainProfileSample> Samples);

public static class TerrainProfileCalculator
{
    public const double DefaultFrequencyGigahertz = 5.8;
    public const double DefaultFresnelClearancePercent = 60;
    public const double DefaultCustomerHeightFeet = 25;
    public const double DefaultClutterHeightFeet = 50;
    public const double SpeedOfLightMetresPerSecond = 299_792_458;

    private const double MetresPerFoot = 0.3048;
    private const double MinimumPathMetres = 1;
    private const double MaximumPathMetres = 200_000;

    public static int GetSampleCount(double distanceMetres)
    {
        ValidateFiniteRange(distanceMetres, nameof(distanceMetres), MinimumPathMetres, MaximumPathMetres);
        return Math.Min(511, Math.Max(33, (2 * (int)Math.Ceiling(distanceMetres / 60)) + 1));
    }

    public static TerrainProfile Calculate(
        TerrainEndpoint customer,
        TerrainEndpoint tower,
        IReadOnlyList<ElevationSample> elevationSamples,
        TerrainProfileOptions options)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(tower);
        ArgumentNullException.ThrowIfNull(elevationSamples);
        ArgumentNullException.ThrowIfNull(options);
        ValidateEndpoint(customer, nameof(customer));
        ValidateEndpoint(tower, nameof(tower));
        ValidateFiniteRange(options.FrequencyGigahertz, nameof(options.FrequencyGigahertz), 0.001, 1000);
        ValidateFiniteRange(options.FresnelClearancePercent, nameof(options.FresnelClearancePercent), 0, 100);
        ValidateFiniteRange(options.ClutterHeightFeet, nameof(options.ClutterHeightFeet), 0, 300);
        if (!Enum.IsDefined(options.EarthModel))
        {
            throw new ArgumentOutOfRangeException(nameof(options.EarthModel));
        }

        var distanceMetres = GeoMath.DistanceKilometres(customer.Location, tower.Location) * 1000;
        var expectedCount = GetSampleCount(distanceMetres);
        if (elevationSamples.Count != expectedCount)
        {
            throw new ArgumentException("The elevation response is incomplete. No clearance assessment was made.", nameof(elevationSamples));
        }

        var spacingMetres = distanceMetres / (expectedCount - 1);
        ValidateElevationSamples(customer.Location, tower.Location, elevationSamples, spacingMetres);
        var customerHeightMetres = customer.HeightFeet * MetresPerFoot;
        var towerHeightMetres = tower.HeightFeet * MetresPerFoot;
        var clutterHeightMetres = options.ClutterHeightFeet * MetresPerFoot;
        var startAntennaMetres = elevationSamples[0].ElevationMetres + customerHeightMetres;
        var endAntennaMetres = elevationSamples[^1].ElevationMetres + towerHeightMetres;
        var wavelengthMetres = SpeedOfLightMetresPerSecond / (options.FrequencyGigahertz * 1_000_000_000);
        double? effectiveEarthRadiusFactor = options.EarthModel switch
        {
            EarthModel.StandardFourThirds => 4d / 3,
            EarthModel.Geometric => 1,
            EarthModel.Flat => null,
            _ => throw new ArgumentOutOfRangeException(nameof(options.EarthModel)),
        };
        var effectiveEarthRadiusMetres = effectiveEarthRadiusFactor * GeoMath.EarthRadiusKilometres * 1000;
        var profileSamples = new TerrainProfileSample[expectedCount];

        for (var index = 0; index < expectedCount; index++)
        {
            var fraction = index / (double)(expectedCount - 1);
            var pathDistanceMetres = distanceMetres * fraction;
            var curvatureBulgeMetres = effectiveEarthRadiusFactor is null
                ? 0
                : pathDistanceMetres * (distanceMetres - pathDistanceMetres) / (2 * effectiveEarthRadiusMetres!.Value);
            var lineOfSightMetres = startAntennaMetres + ((endAntennaMetres - startAntennaMetres) * fraction);
            var firstFresnelRadiusMetres = Math.Sqrt(
                wavelengthMetres * pathDistanceMetres * (distanceMetres - pathDistanceMetres) / distanceMetres);
            var referenceRadiusMetres = firstFresnelRadiusMetres * options.FresnelClearancePercent / 100;
            var terrainMetres = elevationSamples[index].ElevationMetres + curvatureBulgeMetres;
            var clutterTopMetres = terrainMetres + clutterHeightMetres;
            var clearanceMetres = lineOfSightMetres - clutterTopMetres;
            var referenceMarginMetres = clearanceMetres - referenceRadiusMetres;
            profileSamples[index] = new TerrainProfileSample(
                index,
                elevationSamples[index].Location,
                pathDistanceMetres,
                elevationSamples[index].ElevationMetres,
                curvatureBulgeMetres,
                terrainMetres,
                clutterTopMetres,
                lineOfSightMetres,
                firstFresnelRadiusMetres,
                referenceRadiusMetres,
                clearanceMetres,
                referenceMarginMetres,
                CategorizeClearance(clearanceMetres));
        }

        var knownResolutions = elevationSamples
            .Where(sample => sample.ResolutionMetres is not null)
            .Select(sample => sample.ResolutionMetres!.Value)
            .ToArray();
        return new TerrainProfile(
            distanceMetres,
            GeoMath.InitialBearingDegrees(customer.Location, tower.Location),
            spacingMetres,
            options.FrequencyGigahertz,
            options.FresnelClearancePercent,
            options.EarthModel,
            effectiveEarthRadiusFactor,
            customerHeightMetres,
            towerHeightMetres,
            clutterHeightMetres,
            knownResolutions.Length == 0 ? null : knownResolutions.Max(),
            elevationSamples.Count - knownResolutions.Length,
            profileSamples.Min(sample => sample.ClearanceMetres) > 0,
            profileSamples.Min(sample => sample.FresnelReferenceMarginMetres) > 0,
            profileSamples.MinBy(sample => sample.ClearanceMetres)!,
            profileSamples.MinBy(sample => sample.FresnelReferenceMarginMetres)!,
            profileSamples);
    }

    public static ClearanceCategory CategorizeClearance(double clearanceMetres)
    {
        if (!double.IsFinite(clearanceMetres))
        {
            throw new ArgumentOutOfRangeException(nameof(clearanceMetres));
        }

        return clearanceMetres switch
        {
            <= 0 => ClearanceCategory.Obstruction,
            <= 3.048 => ClearanceCategory.TenFeetOrLess,
            <= 9.144 => ClearanceCategory.ThirtyFeetOrLess,
            <= 15.24 => ClearanceCategory.FiftyFeetOrLess,
            _ => ClearanceCategory.MoreThanFiftyFeet,
        };
    }

    private static void ValidateEndpoint(TerrainEndpoint endpoint, string parameterName)
    {
        if (!endpoint.Location.IsValid)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The endpoint location is invalid.");
        }

        ValidateFiniteRange(endpoint.HeightFeet, parameterName, 0, 32_808.4);
    }

    private static void ValidateElevationSamples(
        GeoCoordinate customer,
        GeoCoordinate tower,
        IReadOnlyList<ElevationSample> samples,
        double spacingMetres)
    {
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            if (!sample.Location.IsValid ||
                !double.IsFinite(sample.ElevationMetres) ||
                sample.ElevationMetres is < -12_000 or > 10_000 ||
                (sample.ResolutionMetres is not null &&
                    (!double.IsFinite(sample.ResolutionMetres.Value) || sample.ResolutionMetres is < 0 or > 100_000)))
            {
                throw new ArgumentException("The elevation response contains an invalid sample. No clearance assessment was made.", nameof(samples));
            }

            var expected = GeoMath.InterpolateGeodesic(customer, tower, index / (double)(samples.Count - 1));
            var differenceMetres = GeoMath.DistanceKilometres(sample.Location, expected) * 1000;
            var toleranceMetres = Math.Max(0.02, Math.Min(3, spacingMetres / 4));
            if (differenceMetres > toleranceMetres)
            {
                throw new ArgumentException("The elevation samples do not match the selected path. No clearance assessment was made.", nameof(samples));
            }
        }
    }

    private static void ValidateFiniteRange(double value, string parameterName, double minimum, double maximum)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
