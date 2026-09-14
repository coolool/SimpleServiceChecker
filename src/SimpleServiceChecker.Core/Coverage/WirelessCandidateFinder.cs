namespace SimpleServiceChecker.Core.Coverage;

public sealed record WirelessCandidate(TowerCoverage Tower, double DistanceKilometres);

public static class WirelessCandidateFinder
{
    private const double BoundaryToleranceKilometres = 1e-9;

    public static IReadOnlyList<WirelessCandidate> Find(
        IEnumerable<TowerCoverage> towers,
        GeoCoordinate customerLocation)
    {
        ArgumentNullException.ThrowIfNull(towers);
        return !customerLocation.IsValid
            ? throw new ArgumentOutOfRangeException(nameof(customerLocation), "The customer coordinate is outside the valid latitude or longitude range.")
            : [.. towers
            .Where(tower => tower.CanProduceCandidate && tower.Location.IsValid)
            .Select(tower => new
            {
                Tower = tower,
                Distance = GeoMath.DistanceKilometres(customerLocation, tower.Location),
                Bearing = GeoMath.InitialBearingDegrees(tower.Location, customerLocation),
            })
            .Where(result => result.Distance <= result.Tower.RangeKilometres + BoundaryToleranceKilometres)
            .Where(result => result.Tower.IsOmnidirectional ||
                SectorMath.ContainsBearing(
                    result.Tower.ApDirectionDegrees,
                    result.Tower.ApCoverageDegrees,
                    result.Bearing))
            .OrderBy(result => result.Distance)
            .ThenBy(result => result.Tower.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Tower.SourceRowNumber)
            .Select(result => new WirelessCandidate(result.Tower, result.Distance))];
    }
}

public static class GeoMath
{
    public const double EarthRadiusKilometres = 6371.0088;

    public static double DistanceKilometres(GeoCoordinate first, GeoCoordinate second)
    {
        EnsureValid(first, nameof(first));
        EnsureValid(second, nameof(second));

        var latitudeDelta = ToRadians(second.Latitude - first.Latitude);
        var longitudeDelta = ToRadians(second.Longitude - first.Longitude);
        var firstLatitude = ToRadians(first.Latitude);
        var secondLatitude = ToRadians(second.Latitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
            (Math.Cos(firstLatitude) * Math.Cos(secondLatitude) *
            Math.Pow(Math.Sin(longitudeDelta / 2), 2));

        return 2 * EarthRadiusKilometres * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    public static double InitialBearingDegrees(GeoCoordinate origin, GeoCoordinate destination)
    {
        EnsureValid(origin, nameof(origin));
        EnsureValid(destination, nameof(destination));

        var originLatitude = ToRadians(origin.Latitude);
        var destinationLatitude = ToRadians(destination.Latitude);
        var longitudeDelta = ToRadians(destination.Longitude - origin.Longitude);
        var y = Math.Sin(longitudeDelta) * Math.Cos(destinationLatitude);
        var x = (Math.Cos(originLatitude) * Math.Sin(destinationLatitude)) -
            (Math.Sin(originLatitude) * Math.Cos(destinationLatitude) * Math.Cos(longitudeDelta));

        return NormalizeDegrees(ToDegrees(Math.Atan2(y, x)));
    }

    public static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    public static GeoCoordinate InterpolateGeodesic(
        GeoCoordinate origin,
        GeoCoordinate destination,
        double fraction)
    {
        EnsureValid(origin, nameof(origin));
        EnsureValid(destination, nameof(destination));
        if (!double.IsFinite(fraction) || fraction < 0 || fraction > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }

        if (fraction == 0)
        {
            return origin;
        }

        if (fraction == 1)
        {
            return destination;
        }

        var angle = DistanceKilometres(origin, destination) / EarthRadiusKilometres;
        if (angle < 1e-12 || Math.Abs(Math.Sin(angle)) < 1e-12)
        {
            throw new ArgumentException("The path cannot be interpolated.", nameof(destination));
        }

        var originLatitude = ToRadians(origin.Latitude);
        var originLongitude = ToRadians(origin.Longitude);
        var destinationLatitude = ToRadians(destination.Latitude);
        var destinationLongitude = ToRadians(destination.Longitude);
        var originWeight = Math.Sin((1 - fraction) * angle) / Math.Sin(angle);
        var destinationWeight = Math.Sin(fraction * angle) / Math.Sin(angle);
        var x = (originWeight * Math.Cos(originLatitude) * Math.Cos(originLongitude)) +
            (destinationWeight * Math.Cos(destinationLatitude) * Math.Cos(destinationLongitude));
        var y = (originWeight * Math.Cos(originLatitude) * Math.Sin(originLongitude)) +
            (destinationWeight * Math.Cos(destinationLatitude) * Math.Sin(destinationLongitude));
        var z = (originWeight * Math.Sin(originLatitude)) +
            (destinationWeight * Math.Sin(destinationLatitude));

        return new GeoCoordinate(
            ToDegrees(Math.Atan2(z, Math.Sqrt((x * x) + (y * y)))),
            WrapLongitude(ToDegrees(Math.Atan2(y, x))));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    private static double ToDegrees(double radians)
    {
        return radians * 180 / Math.PI;
    }

    private static double WrapLongitude(double longitude) =>
        ((((longitude + 180) % 360) + 360) % 360) - 180;

    private static void EnsureValid(GeoCoordinate coordinate, string parameterName)
    {
        if (!coordinate.IsValid)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The coordinate is outside the valid latitude or longitude range.");
        }
    }
}

public static class SectorMath
{
    private const double BoundaryToleranceDegrees = 1e-9;

    public static bool ContainsBearing(double? directionDegrees, double coverageDegrees, double bearingDegrees)
    {
        if (!double.IsFinite(coverageDegrees) || coverageDegrees <= 0 || coverageDegrees > 360)
        {
            throw new ArgumentOutOfRangeException(nameof(coverageDegrees));
        }

        if (!double.IsFinite(bearingDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(bearingDegrees));
        }

        if (coverageDegrees >= 360)
        {
            return true;
        }

        if (directionDegrees is null || !double.IsFinite(directionDegrees.Value))
        {
            return false;
        }

        var direction = GeoMath.NormalizeDegrees(directionDegrees.Value);
        var bearing = GeoMath.NormalizeDegrees(bearingDegrees);
        var difference = Math.Abs(direction - bearing);
        var shortestDifference = Math.Min(difference, 360 - difference);
        return shortestDifference <= (coverageDegrees / 2) + BoundaryToleranceDegrees;
    }
}
