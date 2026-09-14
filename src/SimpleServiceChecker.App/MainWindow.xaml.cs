using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using SimpleServiceChecker.App.Bridge;
using SimpleServiceChecker.App.Infrastructure;
using SimpleServiceChecker.App.Security;
using SimpleServiceChecker.Core.Coverage;
using SimpleServiceChecker.Core.Terrain;
using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.App;

public partial class MainWindow : Window
{
    private readonly GoogleKeyStore googleKeyStore = new(AppPaths.GoogleKeyFile);
    private readonly WorkbookActivationService workbookActivationService = new(
        AppPaths.CurrentWorkbookFile,
        AppPaths.CurrentManifestFile,
        AppPaths.PreviousWorkbookFile,
        AppPaths.PreviousManifestFile,
        AppPaths.TemporaryDirectory);
    private readonly SemaphoreSlim workbookOperationLock = new(1, 1);
    private CoverageDataset? activeDataset;
    private CoverageWorkbookManifest? activeManifest;
    private string? selectedWorkbookSource;
    private CancellationTokenSource? locationSelectionCancellation;
    private CancellationTokenSource? terrainAnalysisCancellation;
    private ActiveLocationSelection? activeLocationSelection;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            AppPaths.EnsureCreated();
            workbookActivationService.CleanupTemporaryFiles();
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
        catch (WebView2RuntimeNotFoundException)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            _ = MessageBox.Show(
                this,
                "Microsoft Edge WebView2 Runtime is required. Re-run the Starling Service Checker installer while connected to the internet. If installation is blocked, contact your IT support or download WebView2 directly from Microsoft:\n\n" + AppConstants.WebView2DownloadUri,
                "Starling Service Checker requires WebView2",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception exception)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            _ = MessageBox.Show(
                this,
                $"The application could not start its embedded browser.\n\n{exception.Message}",
                "Starling Service Checker",
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
                case BridgeMessageTypes.ActivateWorkbook:
                    await HandleActivateWorkbookAsync(message.RequestId);
                    break;
                case BridgeMessageTypes.ReloadWorkbook:
                    await HandleReloadWorkbookAsync(message.RequestId);
                    break;
                case BridgeMessageTypes.OpenCurrentWorkbook:
                    await HandleOpenCurrentWorkbookAsync(message.RequestId);
                    break;
                case BridgeMessageTypes.RestorePreviousWorkbook:
                    await HandleRestorePreviousWorkbookAsync(message.RequestId);
                    break;
                case BridgeMessageTypes.OpenUpdateLocation:
                    OpenExternalHttpsUri(AppConstants.UpdateLocationUri);
                    await SendAsync("updateLocationOpened", message.RequestId, new { });
                    break;
                case BridgeMessageTypes.SelectLocation:
                    await HandleSelectLocationAsync(message);
                    break;
                case BridgeMessageTypes.ClearLocation:
                    CancelLocationSelection();
                    CancelTerrainAnalysis();
                    activeLocationSelection = null;
                    await SendAsync("locationCleared", message.RequestId, new { });
                    break;
                case BridgeMessageTypes.AnalyzeTerrain:
                    await HandleAnalyzeTerrainAsync(message);
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
            canReloadWorkbook = selectedWorkbookSource is not null,
            canRestorePrevious = workbookActivationService.HasPreviousWorkbook,
            applicationSchemaVersion = CoverageWorkbookManifest.CurrentSchemaVersion,
            applicationOrigin = $"https://{AppConstants.VirtualHostName}",
            version = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "development",
        });

        if (current is not null && !current.LoadResult.IsValid)
        {
            await SendAsync("workbookUnavailable", requestId, new
            {
                message = "The saved coverage workbook could not be loaded. Choose the spreadsheet again in Settings.",
                errors = current.LoadResult.Errors.Select(issue => issue.Message),
                canRestorePrevious = workbookActivationService.HasPreviousWorkbook,
            });
        }
        else if (activeDataset is not null)
        {
            await SendCoverageDatasetAsync(requestId, recoveredFromPrevious: current?.RecoveredFromPrevious == true);
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
        var sourceFile = ChooseWorkbookFile("Choose coverage spreadsheet");
        if (sourceFile is null)
        {
            await SendAsync("workbookSelectionCancelled", requestId, new { });
            return;
        }

        selectedWorkbookSource = sourceFile;
        await SendAsync("workbookSelected", requestId, new
        {
            fileName = Path.GetFileName(sourceFile),
            canActivate = true,
            canReloadWorkbook = true,
        });
    }

    private async Task HandleActivateWorkbookAsync(string requestId)
    {
        if (selectedWorkbookSource is null)
        {
            await SendAsync("workbookRejected", requestId, new
            {
                errors = new[] { "Choose a spreadsheet before validating and activating it." },
                canRestorePrevious = workbookActivationService.HasPreviousWorkbook,
            });
            return;
        }

        await ActivateWorkbookSourceAsync(selectedWorkbookSource, requestId, "activated");
    }

    private async Task HandleReloadWorkbookAsync(string requestId)
    {
        var sourceFile = selectedWorkbookSource;
        if (sourceFile is null || !File.Exists(sourceFile))
        {
            sourceFile = ChooseWorkbookFile("Choose the source spreadsheet to reload");
            if (sourceFile is null)
            {
                await SendAsync("workbookSelectionCancelled", requestId, new { });
                return;
            }

            selectedWorkbookSource = sourceFile;
        }

        await ActivateWorkbookSourceAsync(sourceFile, requestId, "reloaded");
    }

    private async Task HandleOpenCurrentWorkbookAsync(string requestId)
    {
        if (!await workbookOperationLock.WaitAsync(0))
        {
            await SendWorkbookBusyAsync(requestId);
            return;
        }

        try
        {
            var inspectionFile = await workbookActivationService.CreateReadOnlyInspectionCopyAsync();
            if (inspectionFile is null)
            {
                await SendAsync("workbookOperationFailed", requestId, new
                {
                    message = "There is no valid active workbook to open.",
                    canRestorePrevious = workbookActivationService.HasPreviousWorkbook,
                });
                return;
            }

            _ = Process.Start(new ProcessStartInfo(inspectionFile) { UseShellExecute = true });
            await SendAsync("workbookOpened", requestId, new
            {
                message = "Opened a read-only copy. Changes cannot alter active coverage.",
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            await SendAsync("workbookOperationFailed", requestId, new
            {
                message = "Windows could not open the spreadsheet. The active coverage remains available.",
                canRestorePrevious = workbookActivationService.HasPreviousWorkbook,
            });
        }
        finally
        {
            _ = workbookOperationLock.Release();
        }
    }

    private async Task HandleRestorePreviousWorkbookAsync(string requestId)
    {
        if (!await workbookOperationLock.WaitAsync(0))
        {
            await SendWorkbookBusyAsync(requestId);
            return;
        }

        try
        {
            await SendAsync("workbookOperationStarted", requestId, new { message = "Validating the previous workbook before restoration…" });
            var result = await workbookActivationService.RestorePreviousAsync();
            if (result is null)
            {
                await SendAsync("workbookOperationFailed", requestId, new
                {
                    message = "No previous workbook is available to restore.",
                    canRestorePrevious = false,
                });
                return;
            }

            if (!result.LoadResult.IsValid || result.LoadResult.Dataset is null || result.Manifest is null)
            {
                await SendWorkbookRejectedAsync(requestId, result, null);
                return;
            }

            selectedWorkbookSource = null;
            ApplyActiveWorkbook(result);
            await SendCoverageDatasetAsync(requestId, result.LoadResult.Warnings, operation: "restored", resetLocation: true);
        }
        finally
        {
            _ = workbookOperationLock.Release();
        }
    }

    private async Task ActivateWorkbookSourceAsync(string sourceFile, string requestId, string operation)
    {
        if (!await workbookOperationLock.WaitAsync(0))
        {
            await SendWorkbookBusyAsync(requestId);
            return;
        }

        try
        {
            await SendAsync("workbookOperationStarted", requestId, new
            {
                message = $"Validating {Path.GetFileName(sourceFile)} before activation…",
            });
            var result = await workbookActivationService.ActivateAsync(sourceFile);
            if (!result.LoadResult.IsValid || result.LoadResult.Dataset is null || result.Manifest is null)
            {
                await SendWorkbookRejectedAsync(requestId, result, Path.GetFileName(sourceFile));
                return;
            }

            ApplyActiveWorkbook(result);
            await SendCoverageDatasetAsync(requestId, result.LoadResult.Warnings, operation, resetLocation: true);
        }
        finally
        {
            _ = workbookOperationLock.Release();
        }
    }

    private void ApplyActiveWorkbook(WorkbookActivationResult result)
    {
        CancelLocationSelection();
        CancelTerrainAnalysis();
        activeLocationSelection = null;
        activeDataset = result.LoadResult.Dataset;
        activeManifest = result.Manifest;
    }

    private async Task SendWorkbookRejectedAsync(
        string requestId,
        WorkbookActivationResult result,
        string? fileName)
    {
        await SendAsync("workbookRejected", requestId, new
        {
            fileName,
            errors = result.LoadResult.Errors.Select(issue => issue.Message),
            warnings = result.LoadResult.Warnings.Select(issue => issue.Message),
            canRestorePrevious = workbookActivationService.HasPreviousWorkbook,
        });
    }

    private Task SendWorkbookBusyAsync(string requestId) =>
        SendAsync("workbookOperationFailed", requestId, new
        {
            message = "Another spreadsheet operation is already in progress.",
            canRestorePrevious = workbookActivationService.HasPreviousWorkbook,
        });

    private string? ChooseWorkbookFile(string title)
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Excel workbooks (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
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
        CancelTerrainAnalysis();
        activeLocationSelection = null;
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
        cancellationToken.ThrowIfCancellationRequested();
        activeLocationSelection = new ActiveLocationSelection(
            request!.Revision,
            request.Location,
            Candidates.Select(candidate => candidate.Tower.SourceRowNumber).ToHashSet());

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
                terrainSampleCount = GetTerrainSampleCount(candidate.DistanceKilometres * 1000),
            }),
        });
    }

    private async Task HandleAnalyzeTerrainAsync(BridgeMessage message)
    {
        if (!TerrainAnalysisRequestParser.TryParse(message.Payload, out var request, out var error))
        {
            await SendAsync("terrainProfileRejected", message.RequestId, new { message = error });
            return;
        }

        if (activeDataset is null ||
            activeLocationSelection is null ||
            request!.LocationRevision != activeLocationSelection.Revision ||
            !activeLocationSelection.CandidateRows.Contains(request.TowerRowNumber))
        {
            await SendAsync("terrainProfileRejected", message.RequestId, new
            {
                plotRevision = request!.PlotRevision,
                message = "The customer location or tower changed. Open the profile again.",
            });
            return;
        }

        var selection = activeLocationSelection;
        var tower = activeDataset.Towers.FirstOrDefault(candidate => candidate.SourceRowNumber == request.TowerRowNumber);
        if (tower is null)
        {
            await SendAsync("terrainProfileRejected", message.RequestId, new
            {
                plotRevision = request.PlotRevision,
                message = "The selected tower is no longer in the active workbook.",
            });
            return;
        }

        CancelTerrainAnalysis();
        terrainAnalysisCancellation = new CancellationTokenSource();
        var cancellationToken = terrainAnalysisCancellation.Token;
        try
        {
            var profile = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var calculated = TerrainProfileCalculator.Calculate(
                    new TerrainEndpoint(selection.Location, request.CustomerHeightFeet),
                    new TerrainEndpoint(tower.Location, request.TowerHeightFeet),
                    request.Samples,
                    request.Options);
                cancellationToken.ThrowIfCancellationRequested();
                return calculated;
            }, cancellationToken);

            await SendAsync("terrainProfileResult", message.RequestId, new
            {
                locationRevision = request.LocationRevision,
                plotRevision = request.PlotRevision,
                towerRowNumber = request.TowerRowNumber,
                towerName = tower.Name,
                profile.DistanceMetres,
                profile.BearingDegrees,
                profile.SampleSpacingMetres,
                profile.FrequencyGigahertz,
                profile.FresnelClearancePercent,
                earthModel = EarthModelName(profile.EarthModel),
                profile.EffectiveEarthRadiusFactor,
                profile.CustomerHeightMetres,
                profile.TowerHeightMetres,
                profile.ClutterHeightMetres,
                profile.MaximumResolutionMetres,
                profile.UnknownResolutionCount,
                profile.IsLineOfSightClear,
                profile.IsFresnelReferenceClear,
                minimumClearanceMetres = profile.MinimumClearance.ClearanceMetres,
                minimumClearanceDistanceMetres = profile.MinimumClearance.DistanceMetres,
                minimumFresnelMarginMetres = profile.MinimumFresnelReferenceMargin.FresnelReferenceMarginMetres,
                minimumFresnelMarginDistanceMetres = profile.MinimumFresnelReferenceMargin.DistanceMetres,
                samples = profile.Samples.Select(sample => new
                {
                    sample.Index,
                    latitude = sample.Location.Latitude,
                    longitude = sample.Location.Longitude,
                    sample.DistanceMetres,
                    sample.RawElevationMetres,
                    sample.CurvatureBulgeMetres,
                    sample.TerrainMetres,
                    sample.ClutterTopMetres,
                    sample.LineOfSightMetres,
                    sample.FirstFresnelRadiusMetres,
                    sample.FresnelReferenceRadiusMetres,
                    sample.ClearanceMetres,
                    sample.FresnelReferenceMarginMetres,
                    clearanceCategory = ClearanceCategoryName(sample.ClearanceCategory),
                }),
            });
        }
        catch (ArgumentException)
        {
            await SendAsync("terrainProfileRejected", message.RequestId, new
            {
                plotRevision = request.PlotRevision,
                message = "The elevation samples or profile settings are invalid. No clearance assessment was made.",
            });
        }
    }

    private async Task SendCoverageDatasetAsync(
        string requestId,
        IReadOnlyList<WorkbookValidationIssue>? warnings = null,
        string? operation = null,
        bool resetLocation = false,
        bool recoveredFromPrevious = false)
    {
        if (activeDataset is null)
        {
            return;
        }

        await SendAsync("coverageLoaded", requestId, new
        {
            fileName = activeDataset.SourceFileName,
            activatedAtUtc = activeManifest?.ActivatedAtUtc,
            schemaVersion = activeManifest?.SchemaVersion,
            applicationSchemaVersion = CoverageWorkbookManifest.CurrentSchemaVersion,
            towerCount = activeDataset.Towers.Count,
            fibreRangeCount = activeDataset.FibreRanges.Count,
            warningCount = activeManifest?.WarningCount ?? warnings?.Count ?? 0,
            warnings = warnings?.Select(issue => issue.Message) ?? activeManifest?.Warnings ?? [],
            canReloadWorkbook = selectedWorkbookSource is not null,
            canRestorePrevious = workbookActivationService.HasPreviousWorkbook,
            operation,
            resetLocation,
            recoveredFromPrevious,
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

    private void CancelTerrainAnalysis()
    {
        terrainAnalysisCancellation?.Cancel();
        terrainAnalysisCancellation?.Dispose();
        terrainAnalysisCancellation = null;
    }

    private static int GetTerrainSampleCount(double distanceMetres) =>
        distanceMetres is >= 1 and <= 200_000
            ? TerrainProfileCalculator.GetSampleCount(distanceMetres)
            : 0;

    private static string EarthModelName(EarthModel earthModel) => earthModel switch
    {
        EarthModel.StandardFourThirds => "standard",
        EarthModel.Geometric => "geometric",
        EarthModel.Flat => "flat",
        _ => "unknown",
    };

    private static string ClearanceCategoryName(ClearanceCategory category) => category switch
    {
        ClearanceCategory.Obstruction => "black",
        ClearanceCategory.TenFeetOrLess => "red",
        ClearanceCategory.ThirtyFeetOrLess => "orange",
        ClearanceCategory.FiftyFeetOrLess => "yellow",
        ClearanceCategory.MoreThanFiftyFeet => "green",
        _ => "black",
    };

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
        CancelTerrainAnalysis();
        base.OnClosed(e);
    }

    private sealed record ActiveLocationSelection(
        long Revision,
        GeoCoordinate Location,
        IReadOnlySet<int> CandidateRows);
}
