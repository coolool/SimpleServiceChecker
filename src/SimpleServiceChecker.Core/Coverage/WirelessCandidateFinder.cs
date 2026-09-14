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
        if (!customerLocation.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(customerLocation), "The customer coordinate is outside the valid latitude or longitude range.");
        }

        return towers
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
            .Select(result => new WirelessCandidate(result.Tower, result.Distance))
            .ToArray();
    }
}

public static class GeoMath
{
    private const double EarthRadiusKilometres = 6371.0088;

    public static double DistanceKilometres(GeoCoordinate first, GeoCoordinate second)
    {
        EnsureValid(first, nameof(first));
        EnsureValid(second, nameof(second));

        var latitudeDelta = ToRadians(second.Latitude - first.Latitude);
        var longitudeDelta = ToRadians(second.Longitude - first.Longitude);
        var firstLatitude = ToRadians(first.Latitude);
        var secondLatitude = ToRadians(second.Latitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
            Math.Cos(firstLatitude) * Math.Cos(secondLatitude) *
            Math.Pow(Math.Sin(longitudeDelta / 2), 2);

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
        var x = Math.Cos(originLatitude) * Math.Sin(destinationLatitude) -
            Math.Sin(originLatitude) * Math.Cos(destinationLatitude) * Math.Cos(longitudeDelta);

        return NormalizeDegrees(ToDegrees(Math.Atan2(y, x)));
    }

    public static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;

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
