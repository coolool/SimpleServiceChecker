using System.Text.Json;

namespace SimpleServiceChecker.App.Bridge;

public sealed record BridgeMessage(int Version, string Type, string RequestId, JsonElement Payload);
