using SimpleServiceChecker.App.Bridge;
using SimpleServiceChecker.App.Infrastructure;
using SimpleServiceChecker.App.Security;

namespace SimpleServiceChecker.App.Tests;

public sealed class BridgeMessageRouterTests
{
    [Fact]
    public void TryParse_AcceptsAnAllowedVersionedMessage()
    {
        const string json = """
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
    public void GoogleKeyStore_RoundTripsAndClearsForTheCurrentWindowsUser()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ssc-key-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "google-key.bin");
        var store = new GoogleKeyStore(path);

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
    [InlineData("{\"version\":2,\"type\":\"chooseWorkbook\",\"requestId\":\"request-1\",\"payload\":{}}")]
    [InlineData("{\"version\":1,\"type\":\"deleteEverything\",\"requestId\":\"request-1\",\"payload\":{}}")]
    [InlineData("{\"version\":1,\"type\":\"chooseWorkbook\",\"requestId\":\"\",\"payload\":{}}")]
    [InlineData("{\"version\":1,\"type\":\"chooseWorkbook\",\"requestId\":\"request-1\",\"payload\":[]}")]
    public void TryParse_RejectsInvalidMessages(string json)
    {
        var success = BridgeMessageRouter.TryParse(json, out var message, out var error);

        Assert.False(success);
        Assert.Null(message);
        Assert.NotEmpty(error);
    }
}
