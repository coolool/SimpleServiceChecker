using System.Text.Json;
using SimpleServiceChecker.Core.Coverage;

namespace SimpleServiceChecker.App.Bridge;

public sealed record LocationSelectionRequest(
    long Revision,
    GeoCoordinate Location,
    string Source,
    string? FormattedAddress,
    FibreAddress FibreAddress);

public static class LocationSelectionRequestParser
{
    public static bool TryParse(JsonElement payload, out LocationSelectionRequest? request, out string error)
    {
        request = null;
        error = string.Empty;

        if (!payload.TryGetProperty("revision", out var revisionElement) ||
            !revisionElement.TryGetInt64(out var revision) || revision < 1 ||
            !TryGetFiniteDouble(payload, "latitude", out var latitude) ||
            !TryGetFiniteDouble(payload, "longitude", out var longitude))
        {
            error = "Choose a valid customer location.";
            return false;
        }

        GeoCoordinate location = new GeoCoordinate(latitude, longitude);
        if (!location.IsValid)
        {
            error = "The customer latitude or longitude is outside the valid range.";
            return false;
        }

        var source = GetOptionalString(payload, "source") ?? "unknown";
        if (source.Length > 30)
        {
            error = "The location source is invalid.";
            return false;
        }

        var addressElement = payload.TryGetProperty("address", out var parsedAddress) &&
            parsedAddress.ValueKind == JsonValueKind.Object
                ? parsedAddress
                : default;
        var civicNumber = TryGetOptionalInteger(addressElement, "civicNumber");
        var route = GetOptionalString(addressElement, "route");
        _ = AddressNormalizer.TrySplitStreetRoute(route, out var streetName, out var streetType);

        request = new LocationSelectionRequest(
            revision,
            location,
            source,
            GetOptionalString(addressElement, "formattedAddress"),
            new FibreAddress(
                civicNumber,
                streetName,
                streetType,
                GetOptionalString(addressElement, "city"),
                GetOptionalString(addressElement, "postalCode")));
        return true;
    }

    private static bool TryGetFiniteDouble(JsonElement element, string propertyName, out double value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value) &&
            double.IsFinite(value);
    }

    private static int? TryGetOptionalInteger(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number)
            ? number
            : property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), out number)
                ? number
                : null;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
