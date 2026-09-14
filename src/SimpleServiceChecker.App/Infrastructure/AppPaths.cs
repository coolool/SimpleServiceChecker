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

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(CoverageDirectory);
        Directory.CreateDirectory(TemporaryDirectory);
        Directory.CreateDirectory(WebViewDataDirectory);
    }
}
