using System.Text.Json;

namespace SimpleServiceChecker.App.Bridge;

public static class BridgeMessageRouter
{
    public const int CurrentVersion = 1;

    private static readonly HashSet<string> AllowedTypes =
    [
        BridgeMessageTypes.AppReady,
        BridgeMessageTypes.SaveGoogleKey,
        BridgeMessageTypes.ClearGoogleKey,
        BridgeMessageTypes.ChooseWorkbook,
        BridgeMessageTypes.SelectLocation,
        BridgeMessageTypes.ClearLocation,
    ];

    public static bool TryParse(string json, out BridgeMessage? message, out string error)
    {
        message = null;
        error = string.Empty;

        try
        {
            message = BridgeJson.Deserialize<BridgeMessage>(json);
        }
        catch (JsonException)
        {
            error = "The application received a malformed message.";
            return false;
        }

        if (message is null || message.Version != CurrentVersion)
        {
            message = null;
            error = "The application received an unsupported message version.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.RequestId) || message.RequestId.Length > 100)
        {
            message = null;
            error = "The application received an invalid request identifier.";
            return false;
        }

        if (!AllowedTypes.Contains(message.Type))
        {
            message = null;
            error = "The application received an unsupported request.";
            return false;
        }

        if (message.Payload.ValueKind != JsonValueKind.Object)
        {
            message = null;
            error = "The application received an invalid request payload.";
            return false;
        }

        return true;
    }
}
