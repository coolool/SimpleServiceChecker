using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using SimpleServiceChecker.App.Bridge;
using SimpleServiceChecker.App.Infrastructure;
using SimpleServiceChecker.App.Security;
using SimpleServiceChecker.Core.Coverage;
using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.App;

public partial class MainWindow : Window
{
    private readonly GoogleKeyStore googleKeyStore = new(AppPaths.GoogleKeyFile);
    private readonly WorkbookActivationService workbookActivationService = new(
        AppPaths.CurrentWorkbookFile,
        AppPaths.CurrentManifestFile,
        AppPaths.TemporaryDirectory);
    private CoverageDataset? activeDataset;
    private CoverageWorkbookManifest? activeManifest;
    private CancellationTokenSource? locationSelectionCancellation;

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
            _ = MessageBox.Show(
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
                case BridgeMessageTypes.SelectLocation:
                    await HandleSelectLocationAsync(message);
                    break;
                case BridgeMessageTypes.ClearLocation:
                    CancelLocationSelection();
                    await SendAsync("locationCleared", message.RequestId, new { });
                    break;
                default:
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer customer location superseded this request.
        }
        catch (Exception exception)
        {
            await SendAsync("operationFailed", null, new { message = exception.Message });
        }
    }

    private async Task HandleAppReadyAsync(string requestId)
    {
        var apiKey = googleKeyStore.TryLoad();
        var current = await workbookActivationService.LoadCurrentAsync();
        activeDataset = current?.LoadResult.IsValid == true ? current.LoadResult.Dataset : null;
        activeManifest = current?.Manifest;
        await SendAsync("setupState", requestId, new
        {
            apiKeyConfigured = apiKey is not null,
            workbookConfigured = activeDataset is not null,
            applicationOrigin = $"https://{AppConstants.VirtualHostName}",
            version = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "development",
        });

        if (current is not null && !current.LoadResult.IsValid)
        {
            await SendAsync("workbookUnavailable", requestId, new
            {
                message = "The saved coverage workbook could not be loaded. Choose the spreadsheet again in Settings.",
                errors = current.LoadResult.Errors.Select(issue => issue.Message),
            });
        }
        else if (activeDataset is not null)
        {
            await SendCoverageDatasetAsync(requestId);
        }

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
        OpenFileDialog dialog = new OpenFileDialog
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

        var result = await workbookActivationService.ActivateAsync(dialog.FileName);
        if (!result.LoadResult.IsValid || result.LoadResult.Dataset is null)
        {
            await SendAsync("workbookRejected", requestId, new
            {
                fileName = Path.GetFileName(dialog.FileName),
                errors = result.LoadResult.Errors.Select(issue => issue.Message),
            });
            return;
        }

        CancelLocationSelection();
        activeDataset = result.LoadResult.Dataset;
        activeManifest = result.Manifest;
        await SendCoverageDatasetAsync(requestId, result.LoadResult.Warnings);
    }

    private async Task HandleSelectLocationAsync(BridgeMessage message)
    {
        if (activeDataset is null)
        {
            await SendAsync("locationRejected", message.RequestId, new
            {
                message = "Choose a valid coverage spreadsheet in Settings first.",
            });
            return;
        }

        if (!LocationSelectionRequestParser.TryParse(message.Payload, out var request, out var error))
        {
            await SendAsync("locationRejected", message.RequestId, new { message = error });
            return;
        }

        CancelLocationSelection();
        locationSelectionCancellation = new CancellationTokenSource();
        var cancellationToken = locationSelectionCancellation.Token;
        var dataset = activeDataset;
        var (Fibre, Candidates) = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fibre = FibreMatcher.Match(dataset.FibreRanges, request!.FibreAddress);
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = WirelessCandidateFinder.Find(dataset.Towers, request.Location);
            cancellationToken.ThrowIfCancellationRequested();
            return (Fibre: fibre, Candidates: candidates);
        }, cancellationToken);

        await SendAsync("locationResult", message.RequestId, new
        {
            revision = request!.Revision,
            latitude = request.Location.Latitude,
            longitude = request.Location.Longitude,
            request.Source,
            request.FormattedAddress,
            fibre = new
            {
                status = Fibre.Status.ToString().ToLowerInvariant(),
                maxSpeeds = Fibre.Match?.MaxSpeeds,
                notes = Fibre.Match?.Notes,
                warnings = Fibre.Warnings,
            },
            candidates = Candidates.Select(candidate => new
            {
                rowNumber = candidate.Tower.SourceRowNumber,
                name = candidate.Tower.Name,
                distanceKilometres = candidate.DistanceKilometres,
                heightFeet = candidate.Tower.HeightFeet,
                rangeKilometres = candidate.Tower.RangeKilometres,
                maxDownloadMbps = candidate.Tower.MaxDownloadMbps,
                maxUploadMbps = candidate.Tower.MaxUploadMbps,
                notes = candidate.Tower.Notes,
                latitude = candidate.Tower.Location.Latitude,
                longitude = candidate.Tower.Location.Longitude,
            }),
        });
    }

    private async Task SendCoverageDatasetAsync(
        string requestId,
        IReadOnlyList<WorkbookValidationIssue>? warnings = null)
    {
        if (activeDataset is null)
        {
            return;
        }

        await SendAsync("coverageLoaded", requestId, new
        {
            fileName = activeDataset.SourceFileName,
            activatedAtUtc = activeManifest?.ActivatedAtUtc,
            towerCount = activeDataset.Towers.Count,
            fibreRangeCount = activeDataset.FibreRanges.Count,
            warningCount = activeManifest?.WarningCount ?? warnings?.Count ?? 0,
            warnings = warnings?.Select(issue => issue.Message) ?? [],
            towers = activeDataset.Towers.Select(tower => new
            {
                rowNumber = tower.SourceRowNumber,
                name = tower.Name,
                latitude = tower.Location.Latitude,
                longitude = tower.Location.Longitude,
                rangeMetres = tower.RangeKilometres * 1000,
                heightFeet = tower.HeightFeet,
                maxDownloadMbps = tower.MaxDownloadMbps,
                maxUploadMbps = tower.MaxUploadMbps,
                tower.ApDirectionDegrees,
                tower.ApCoverageDegrees,
                tower.Notes,
                candidateEnabled = tower.CanProduceCandidate,
            }),
        });
    }

    private void CancelLocationSelection()
    {
        locationSelectionCancellation?.Cancel();
        locationSelectionCancellation?.Dispose();
        locationSelectionCancellation = null;
    }

    private async Task ConfigureGoogleAsync(string apiKey)
    {
        var serializedKey = JsonSerializer.Serialize(apiKey);
        _ = await WebView.CoreWebView2.ExecuteScriptAsync($"window.simpleServiceChecker.configureGoogle({serializedKey});");
    }

    private async Task SendAsync(string type, string? requestId, object payload)
    {
        var message = BridgeJson.Serialize(new
        {
            version = BridgeMessageRouter.CurrentVersion,
            type,
            requestId,
            payload,
        });

        _ = await WebView.CoreWebView2.ExecuteScriptAsync($"window.simpleServiceChecker.receive({JsonSerializer.Serialize(message)});");
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

    private static bool IsAllowedApplicationUri(string uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
        parsed.Scheme == Uri.UriSchemeHttps &&
        string.Equals(parsed.Host, AppConstants.VirtualHostName, StringComparison.OrdinalIgnoreCase);
    }

    private static void OpenExternalHttpsUri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        _ = Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true });
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelLocationSelection();
        base.OnClosed(e);
    }
}
