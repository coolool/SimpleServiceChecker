namespace SimpleServiceChecker.App.Infrastructure;

public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleServiceChecker");

    public static string ConfigDirectory { get; } = Path.Combine(RootDirectory, "config");

    public static string CoverageDirectory { get; } = Path.Combine(RootDirectory, "coverage");

    public static string TemporaryDirectory { get; } = Path.Combine(RootDirectory, "temp");

    public static string WebViewDataDirectory { get; } = Path.Combine(RootDirectory, "webview2");

    public static string GoogleKeyFile { get; } = Path.Combine(ConfigDirectory, "google-key.dat");

    public static string CurrentWorkbookFile { get; } = Path.Combine(CoverageDirectory, "current.xlsx");

    public static string CurrentManifestFile { get; } = Path.Combine(CoverageDirectory, "current-manifest.json");

    public static string PreviousWorkbookFile { get; } = Path.Combine(CoverageDirectory, "previous.xlsx");

    public static string PreviousManifestFile { get; } = Path.Combine(CoverageDirectory, "previous-manifest.json");

    public static void EnsureCreated()
    {
        _ = Directory.CreateDirectory(ConfigDirectory);
        _ = Directory.CreateDirectory(CoverageDirectory);
        _ = Directory.CreateDirectory(TemporaryDirectory);
        _ = Directory.CreateDirectory(WebViewDataDirectory);
    }
}
