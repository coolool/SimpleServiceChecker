using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using SimpleServiceChecker.App.Bridge;
using SimpleServiceChecker.App.Infrastructure;
using SimpleServiceChecker.App.Security;
using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.App;

public partial class MainWindow : Window
{
    private readonly GoogleKeyStore googleKeyStore = new(AppPaths.GoogleKeyFile);

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            AppPaths.EnsureCreated();
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebViewDataDirectory);
            await WebView.EnsureCoreWebView2Async(environment);

            ConfigureWebView(WebView.CoreWebView2);
            var webRoot = Path.Combine(AppContext.BaseDirectory, "Web");
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                AppConstants.VirtualHostName,
                webRoot,
                CoreWebView2HostResourceAccessKind.Allow);

            WebView.Source = new Uri(AppConstants.ApplicationUri);
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            MessageBox.Show(
                this,
                $"The application could not start its embedded browser.\n\n{exception.Message}",
                "Simple Service Checker",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ConfigureWebView(CoreWebView2 core)
    {
        core.Settings.AreDevToolsEnabled = Debugger.IsAttached;
        core.Settings.AreDefaultContextMenusEnabled = Debugger.IsAttached;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;

        core.WebMessageReceived += Core_WebMessageReceived;
        core.NavigationStarting += Core_NavigationStarting;
        core.NewWindowRequested += Core_NewWindowRequested;
    }

    private async void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            if (!IsAllowedApplicationUri(e.Source))
            {
                return;
            }

            if (!BridgeMessageRouter.TryParse(e.WebMessageAsJson, out var message, out var error))
            {
                await SendAsync("bridgeError", null, new { message = error });
                return;
            }

            switch (message!.Type)
            {
                case BridgeMessageTypes.AppReady:
                    await HandleAppReadyAsync(message.RequestId);
                    break;
                case BridgeMessageTypes.SaveGoogleKey:
                    await HandleSaveGoogleKeyAsync(message);
                    break;
                case BridgeMessageTypes.ClearGoogleKey:
                    await HandleClearGoogleKeyAsync(message.RequestId);
                    break;
                case BridgeMessageTypes.ChooseWorkbook:
                    await HandleChooseWorkbookAsync(message.RequestId);
                    break;
            }
        }
        catch (Exception exception)
        {
            await SendAsync("operationFailed", null, new { message = exception.Message });
        }
    }

    private async Task HandleAppReadyAsync(string requestId)
    {
        var apiKey = googleKeyStore.TryLoad();
        await SendAsync("setupState", requestId, new
        {
            apiKeyConfigured = apiKey is not null,
            applicationOrigin = $"https://{AppConstants.VirtualHostName}",
            version = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "development",
        });

        if (apiKey is not null)
        {
            await ConfigureGoogleAsync(apiKey);
        }
    }

    private async Task HandleSaveGoogleKeyAsync(BridgeMessage message)
    {
        if (!message.Payload.TryGetProperty("apiKey", out var apiKeyElement))
        {
            await SendAsync("googleKeyRejected", message.RequestId, new { message = "Enter a Google Maps Platform API key." });
            return;
        }

        var apiKey = apiKeyElement.GetString()?.Trim();
        if (apiKey is null || apiKey.Length is < 20 or > 200)
        {
            await SendAsync("googleKeyRejected", message.RequestId, new { message = "The key format is not valid." });
            return;
        }

        googleKeyStore.Save(apiKey);
        WebView.CoreWebView2.Reload();
        await Task.CompletedTask;
    }

    private async Task HandleClearGoogleKeyAsync(string requestId)
    {
        googleKeyStore.Clear();
        WebView.CoreWebView2.Reload();
        await Task.CompletedTask;
    }

    private async Task HandleChooseWorkbookAsync(string requestId)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose coverage spreadsheet",
            Filter = "Excel workbooks (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
        {
            await SendAsync("workbookSelectionCancelled", requestId, new { });
            return;
        }

        var result = WorkbookProbe.Probe(dialog.FileName);
        await SendAsync("workbookProbeResult", requestId, new
        {
            fileName = Path.GetFileName(dialog.FileName),
            result.IsValid,
            sheets = result.Sheets.Select(sheet => new
            {
                sheet.Name,
                sheet.DataRowCount,
                sheet.ColumnCount,
                sheet.MissingHeaders,
            }),
            result.Errors,
        });
    }

    private async Task ConfigureGoogleAsync(string apiKey)
    {
        var serializedKey = JsonSerializer.Serialize(apiKey);
        await WebView.CoreWebView2.ExecuteScriptAsync($"window.simpleServiceChecker.configureGoogle({serializedKey});");
    }

    private async Task SendAsync(string type, string? requestId, object payload)
    {
        var message = JsonSerializer.Serialize(new
        {
            version = BridgeMessageRouter.CurrentVersion,
            type,
            requestId,
            payload,
        });

        await WebView.CoreWebView2.ExecuteScriptAsync($"window.simpleServiceChecker.receive({JsonSerializer.Serialize(message)});");
    }

    private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (IsAllowedApplicationUri(e.Uri))
        {
            return;
        }

        e.Cancel = true;
        OpenExternalHttpsUri(e.Uri);
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenExternalHttpsUri(e.Uri);
    }

    private static bool IsAllowedApplicationUri(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
        parsed.Scheme == Uri.UriSchemeHttps &&
        string.Equals(parsed.Host, AppConstants.VirtualHostName, StringComparison.OrdinalIgnoreCase);

    private static void OpenExternalHttpsUri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true });
    }
}
