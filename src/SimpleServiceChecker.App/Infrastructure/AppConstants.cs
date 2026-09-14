using System.Reflection;

namespace SimpleServiceChecker.App.Infrastructure;

public static class AppConstants
{
    public const string VirtualHostName = "app.simpleservicechecker.local";
    public const string ApplicationUri = "https://app.simpleservicechecker.local/index.html";
    public const string WebView2DownloadUri = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public static string UpdateLocationUri { get; } = typeof(AppConstants).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => string.Equals(attribute.Key, "UpdateLocationUrl", StringComparison.Ordinal))
        .Value!;
}
