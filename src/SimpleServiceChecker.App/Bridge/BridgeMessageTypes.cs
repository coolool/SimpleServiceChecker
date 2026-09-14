namespace SimpleServiceChecker.App.Bridge;

public static class BridgeMessageTypes
{
    public const string AppReady = "appReady";
    public const string SaveGoogleKey = "saveGoogleKey";
    public const string ClearGoogleKey = "clearGoogleKey";
    public const string ChooseWorkbook = "chooseWorkbook";
    public const string ActivateWorkbook = "activateWorkbook";
    public const string ReloadWorkbook = "reloadWorkbook";
    public const string OpenCurrentWorkbook = "openCurrentWorkbook";
    public const string RestorePreviousWorkbook = "restorePreviousWorkbook";
    public const string SelectLocation = "selectLocation";
    public const string ClearLocation = "clearLocation";
    public const string AnalyzeTerrain = "analyzeTerrain";
}
