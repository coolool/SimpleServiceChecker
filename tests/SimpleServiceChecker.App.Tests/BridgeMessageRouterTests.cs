using System.Text.Json;
using SimpleServiceChecker.App.Bridge;
using SimpleServiceChecker.App.Infrastructure;
using SimpleServiceChecker.App.Security;
using SimpleServiceChecker.Core.Coverage;

namespace SimpleServiceChecker.App.Tests;

public sealed class BridgeMessageRouterTests
{
    [Fact]
    public void TryParse_AcceptsAnAllowedVersionedMessage()
    {
        const string json = /*lang=json,strict*/ """
            {"version":1,"type":"chooseWorkbook","requestId":"request-1","payload":{}}
            """;

        var success = BridgeMessageRouter.TryParse(json, out var message, out var error);

        Assert.True(success, error);
        Assert.NotNull(message);
        Assert.Equal(BridgeMessageTypes.ChooseWorkbook, message.Type);
    }

    [Fact]
    public void ApplicationOrigin_IsStableHttpsVirtualHost()
    {
        Assert.Equal("app.simpleservicechecker.local", AppConstants.VirtualHostName);
        Assert.Equal("https://app.simpleservicechecker.local/index.html", AppConstants.ApplicationUri);
    }

    [Fact]
    public void BridgeJson_UsesCamelCaseForNestedNativeResults()
    {
        var json = BridgeJson.Serialize(new
        {
            IsValid = true,
            Sheets = new[]
            {
                new { Name = "Tower Coverage", DataRowCount = 224 },
            },
        });

        Assert.Contains("\"isValid\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"sheets\"", json, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"Tower Coverage\"", json, StringComparison.Ordinal);
        Assert.Contains("\"dataRowCount\":224", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleKeyStore_RoundTripsAndClearsForTheCurrentWindowsUser()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ssc-key-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "google-key.bin");
        GoogleKeyStore store = new GoogleKeyStore(path);

        try
        {
            const string key = "synthetic-test-key-1234567890";
            store.Save(key);

            Assert.NotEqual(key, File.ReadAllText(path));
            Assert.Equal(key, store.TryLoad());

            store.Clear();
            Assert.Null(store.TryLoad());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData(/*lang=json,strict*/ "{\"version\":2,\"type\":\"chooseWorkbook\",\"requestId\":\"request-1\",\"payload\":{}}")]
    [InlineData(/*lang=json,strict*/ "{\"version\":1,\"type\":\"deleteEverything\",\"requestId\":\"request-1\",\"payload\":{}}")]
    [InlineData(/*lang=json,strict*/ "{\"version\":1,\"type\":\"chooseWorkbook\",\"requestId\":\"\",\"payload\":{}}")]
    [InlineData(/*lang=json,strict*/ "{\"version\":1,\"type\":\"chooseWorkbook\",\"requestId\":\"request-1\",\"payload\":[]}")]
    public void TryParse_RejectsInvalidMessages(string json)
    {
        var success = BridgeMessageRouter.TryParse(json, out var message, out var error);

        Assert.False(success);
        Assert.Null(message);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void LocationParser_TransfersStructuredGoogleAddressForFibreMatching()
    {
        using JsonDocument document = System.Text.Json.JsonDocument.Parse("""
            {
              "revision": 7,
              "latitude": 46.521,
              "longitude": -84.334,
              "source": "places",
              "address": {
                "civicNumber": "125",
                "route": "Queen Street East",
                "city": "Sault Ste. Marie",
                "postalCode": "P6A 1A1",
                "formattedAddress": "125 Queen Street East, Sault Ste. Marie, ON"
              }
            }
            """);

        var success = LocationSelectionRequestParser.TryParse(document.RootElement, out var request, out var error);

        Assert.True(success, error);
        Assert.NotNull(request);
        Assert.Equal(7, request.Revision);
        Assert.Equal(new GeoCoordinate(46.521, -84.334), request.Location);
        Assert.Equal("Queen East", request.FibreAddress.StreetName);
        Assert.Equal("ST", request.FibreAddress.StreetType);
        Assert.Equal(125, request.FibreAddress.CivicNumber);
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"revision\":0,\"latitude\":46,\"longitude\":-84}")]
    [InlineData(/*lang=json,strict*/ "{\"revision\":1,\"latitude\":91,\"longitude\":-84}")]
    [InlineData(/*lang=json,strict*/ "{\"revision\":1,\"latitude\":46,\"longitude\":-181}")]
    [InlineData(/*lang=json,strict*/ "{\"revision\":1,\"latitude\":\"north\",\"longitude\":-84}")]
    public void LocationParser_RejectsInvalidCoordinates(string json)
    {
        using JsonDocument document = System.Text.Json.JsonDocument.Parse(json);

        var success = LocationSelectionRequestParser.TryParse(document.RootElement, out var request, out var error);

        Assert.False(success);
        Assert.Null(request);
        Assert.NotEmpty(error);
    }
}
