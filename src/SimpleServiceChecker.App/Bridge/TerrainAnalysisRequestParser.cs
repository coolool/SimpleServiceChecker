using System.Text.Json;
using SimpleServiceChecker.Core.Coverage;
using SimpleServiceChecker.Core.Terrain;

namespace SimpleServiceChecker.App.Bridge;

public sealed record TerrainAnalysisRequest(
    long LocationRevision,
    long PlotRevision,
    int TowerRowNumber,
    double CustomerHeightFeet,
    double TowerHeightFeet,
    TerrainProfileOptions Options,
    IReadOnlyList<ElevationSample> Samples);

public static class TerrainAnalysisRequestParser
{
    public static bool TryParse(JsonElement payload, out TerrainAnalysisRequest? request, out string error)
    {
        request = null;
        error = string.Empty;
        if (!TryGetPositiveInt64(payload, "locationRevision", out var locationRevision) ||
            !TryGetPositiveInt64(payload, "plotRevision", out var plotRevision) ||
            !TryGetPositiveInt32(payload, "towerRowNumber", out var towerRowNumber) ||
            !TryGetFiniteDouble(payload, "customerHeightFeet", out var customerHeightFeet) ||
            !TryGetFiniteDouble(payload, "towerHeightFeet", out var towerHeightFeet) ||
            !TryGetFiniteDouble(payload, "frequencyGigahertz", out var frequencyGigahertz) ||
            !TryGetFiniteDouble(payload, "fresnelClearancePercent", out var fresnelClearancePercent) ||
            !TryGetFiniteDouble(payload, "clutterHeightFeet", out var clutterHeightFeet) ||
            !TryGetEarthModel(payload, out var earthModel) ||
            !payload.TryGetProperty("samples", out var samplesElement) ||
            samplesElement.ValueKind != JsonValueKind.Array ||
            samplesElement.GetArrayLength() is < 2 or > 512)
        {
            error = "The terrain request is incomplete or invalid.";
            return false;
        }

        var samples = new List<ElevationSample>(samplesElement.GetArrayLength());
        foreach (var sampleElement in samplesElement.EnumerateArray())
        {
            if (!TryGetFiniteDouble(sampleElement, "latitude", out var latitude) ||
                !TryGetFiniteDouble(sampleElement, "longitude", out var longitude) ||
                !TryGetFiniteDouble(sampleElement, "elevationMetres", out var elevationMetres) ||
                !TryGetOptionalFiniteDouble(sampleElement, "resolutionMetres", out var resolutionMetres))
            {
                error = "Google returned an incomplete elevation sample. No clearance assessment was made.";
                return false;
            }

            samples.Add(new ElevationSample(
                new GeoCoordinate(latitude, longitude),
                elevationMetres,
                resolutionMetres));
        }

        request = new TerrainAnalysisRequest(
            locationRevision,
            plotRevision,
            towerRowNumber,
            customerHeightFeet,
            towerHeightFeet,
            new TerrainProfileOptions(
                frequencyGigahertz,
                fresnelClearancePercent,
                earthModel,
                clutterHeightFeet),
            samples);
        return true;
    }

    private static bool TryGetEarthModel(JsonElement payload, out EarthModel earthModel)
    {
        earthModel = default;
        if (!payload.TryGetProperty("earthModel", out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return property.GetString() switch
        {
            "standard" => Assign(EarthModel.StandardFourThirds, out earthModel),
            "geometric" => Assign(EarthModel.Geometric, out earthModel),
            "flat" => Assign(EarthModel.Flat, out earthModel),
            _ => false,
        };
    }

    private static bool Assign(EarthModel value, out EarthModel result)
    {
        result = value;
        return true;
    }

    private static bool TryGetPositiveInt64(JsonElement payload, string propertyName, out long value)
    {
        value = default;
        return payload.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt64(out value) &&
            value > 0;
    }

    private static bool TryGetPositiveInt32(JsonElement payload, string propertyName, out int value)
    {
        value = default;
        return payload.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value) &&
            value > 0;
    }

    private static bool TryGetFiniteDouble(JsonElement payload, string propertyName, out double value)
    {
        value = default;
        return payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value) &&
            double.IsFinite(value);
    }

    private static bool TryGetOptionalFiniteDouble(JsonElement payload, string propertyName, out double? value)
    {
        value = null;
        if (!payload.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out var number) || !double.IsFinite(number))
        {
            return false;
        }

        value = number;
        return true;
    }
}
