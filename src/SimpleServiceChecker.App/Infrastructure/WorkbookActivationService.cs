using System.Security.Cryptography;
using System.Text.Json;
using SimpleServiceChecker.Core.Coverage;
using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.App.Infrastructure;

public sealed record WorkbookActivationResult(
    CoverageWorkbookLoadResult LoadResult,
    CoverageWorkbookManifest? Manifest,
    bool RecoveredFromPrevious = false);

public sealed class WorkbookActivationService(
    string currentWorkbookFile,
    string currentManifestFile,
    string previousWorkbookFile,
    string previousManifestFile,
    string temporaryDirectory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string currentWorkbookFile = currentWorkbookFile;
    private readonly string currentManifestFile = currentManifestFile;
    private readonly string previousWorkbookFile = previousWorkbookFile;
    private readonly string previousManifestFile = previousManifestFile;
    private readonly string temporaryDirectory = temporaryDirectory;

    public bool HasCurrentWorkbook => File.Exists(currentWorkbookFile);

    public bool HasPreviousWorkbook => File.Exists(previousWorkbookFile);

    public void CleanupTemporaryFiles()
    {
        _ = Directory.CreateDirectory(temporaryDirectory);
        foreach (var file in Directory.EnumerateFiles(temporaryDirectory))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (IOException)
            {
                // A read-only inspection copy may still be open in a spreadsheet application.
            }
            catch (UnauthorizedAccessException)
            {
                // Leave an in-use file for the next startup cleanup attempt.
            }
        }
    }

    public async Task<WorkbookActivationResult> ActivateAsync(
        string sourceFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        EnsureDirectories();

        var temporaryWorkbook = NewTemporaryFile("coverage", ".xlsx");
        try
        {
            await CopyReadableSnapshotAsync(sourceFile, temporaryWorkbook, cancellationToken);
            var loadResult = await CoverageWorkbookReader.LoadAsync(temporaryWorkbook, cancellationToken);
            if (!loadResult.IsValid || loadResult.Dataset is null)
            {
                return new WorkbookActivationResult(loadResult, null);
            }

            return await CommitValidatedSnapshotAsync(
                temporaryWorkbook,
                Path.GetFileName(sourceFile),
                loadResult,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Failure($"The spreadsheet could not be activated ({exception.GetType().Name}). The current workbook remains active.");
        }
        finally
        {
            DeleteTemporaryFile(temporaryWorkbook);
        }
    }

    public async Task<WorkbookActivationResult?> LoadCurrentAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectories();
        var current = await LoadSlotAsync(currentWorkbookFile, currentManifestFile, requireValidManifest: true, cancellationToken);
        if (current?.LoadResult.IsValid == true)
        {
            return current;
        }

        var previous = await LoadSlotAsync(previousWorkbookFile, previousManifestFile, requireValidManifest: true, cancellationToken);
        if (previous?.LoadResult.IsValid == true && previous.Manifest is not null)
        {
            try
            {
                await CommitCopyAsync(previousWorkbookFile, currentWorkbookFile, cancellationToken);
                await CommitManifestAsync(previous.Manifest, currentManifestFile, cancellationToken);
                return previous with { RecoveredFromPrevious = true };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return current ?? Failure($"The previous workbook could not be recovered ({exception.GetType().Name}).");
            }
        }

        return current;
    }

    public Task<WorkbookActivationResult?> LoadPreviousAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectories();
        return LoadSlotAsync(previousWorkbookFile, previousManifestFile, requireValidManifest: true, cancellationToken);
    }

    public async Task<WorkbookActivationResult?> RestorePreviousAsync(CancellationToken cancellationToken = default)
    {
        var previous = await LoadPreviousAsync(cancellationToken);
        if (previous is null || !previous.LoadResult.IsValid || previous.LoadResult.Dataset is null || previous.Manifest is null)
        {
            return previous;
        }

        var temporaryWorkbook = NewTemporaryFile("restore", ".xlsx");
        try
        {
            await CopyReadableSnapshotAsync(previousWorkbookFile, temporaryWorkbook, cancellationToken);
            return await CommitValidatedSnapshotAsync(
                temporaryWorkbook,
                previous.Manifest.SourceFileName,
                previous.LoadResult,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Failure($"The previous workbook could not be restored ({exception.GetType().Name}). The current workbook remains active.");
        }
        finally
        {
            DeleteTemporaryFile(temporaryWorkbook);
        }
    }

    public async Task<string?> CreateReadOnlyInspectionCopyAsync(CancellationToken cancellationToken = default)
    {
        var current = await LoadCurrentAsync(cancellationToken);
        if (current?.LoadResult.IsValid != true || current.Manifest is null)
        {
            return null;
        }

        var inspectionFile = NewTemporaryFile("inspection", ".xlsx");
        await CopyReadableSnapshotAsync(currentWorkbookFile, inspectionFile, cancellationToken);
        File.SetAttributes(inspectionFile, File.GetAttributes(inspectionFile) | FileAttributes.ReadOnly);
        return inspectionFile;
    }

    private async Task<WorkbookActivationResult> CommitValidatedSnapshotAsync(
        string snapshotFile,
        string sourceFileName,
        CoverageWorkbookLoadResult loadResult,
        CancellationToken cancellationToken)
    {
        var hash = await ComputeSha256Async(snapshotFile, cancellationToken);
        var manifest = new CoverageWorkbookManifest(
            CoverageWorkbookManifest.CurrentSchemaVersion,
            sourceFileName,
            hash,
            DateTimeOffset.UtcNow,
            loadResult.Dataset!.Towers.Count,
            loadResult.Dataset.FibreRanges.Count,
            loadResult.Warnings.Count,
            [.. loadResult.Warnings.Select(issue => issue.Message)]);

        var currentBackup = await CreateRawSlotBackupAsync(currentWorkbookFile, currentManifestFile, cancellationToken);
        var previousBackup = await CreateRawSlotBackupAsync(previousWorkbookFile, previousManifestFile, cancellationToken);
        try
        {
            var current = await LoadSlotAsync(currentWorkbookFile, currentManifestFile, requireValidManifest: false, cancellationToken);
            if (current?.LoadResult.IsValid == true && current.Manifest is not null)
            {
                await CommitCopyAsync(currentWorkbookFile, previousWorkbookFile, cancellationToken);
                await CommitManifestAsync(current.Manifest, previousManifestFile, cancellationToken);
            }

            await CommitCopyAsync(snapshotFile, currentWorkbookFile, cancellationToken);
            await CommitManifestAsync(manifest, currentManifestFile, cancellationToken);

            return new WorkbookActivationResult(
                loadResult with
                {
                    Dataset = loadResult.Dataset with { SourceFileName = manifest.SourceFileName },
                },
                manifest);
        }
        catch (OperationCanceledException)
        {
            await RestoreRawSlotAsync(currentBackup, currentWorkbookFile, currentManifestFile);
            await RestoreRawSlotAsync(previousBackup, previousWorkbookFile, previousManifestFile);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await RestoreRawSlotAsync(currentBackup, currentWorkbookFile, currentManifestFile);
            await RestoreRawSlotAsync(previousBackup, previousWorkbookFile, previousManifestFile);
            return Failure($"The spreadsheet could not be activated ({exception.GetType().Name}). The current workbook remains active.");
        }
        finally
        {
            currentBackup.Delete();
            previousBackup.Delete();
        }
    }

    private async Task<WorkbookActivationResult?> LoadSlotAsync(
        string workbookFile,
        string manifestFile,
        bool requireValidManifest,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(workbookFile))
        {
            return File.Exists(manifestFile)
                ? Failure("The saved workbook file is missing.")
                : null;
        }

        var loadResult = await CoverageWorkbookReader.LoadAsync(workbookFile, cancellationToken);
        if (!loadResult.IsValid || loadResult.Dataset is null)
        {
            return new WorkbookActivationResult(loadResult, null);
        }

        var manifest = await TryReadManifestAsync(manifestFile, cancellationToken);
        if (manifest is null || !await ManifestMatchesAsync(manifest, workbookFile, loadResult, cancellationToken))
        {
            if (requireValidManifest)
            {
                return Failure("The saved workbook and its integrity manifest do not match.");
            }

            manifest = new CoverageWorkbookManifest(
                CoverageWorkbookManifest.CurrentSchemaVersion,
                loadResult.Dataset.SourceFileName,
                await ComputeSha256Async(workbookFile, cancellationToken),
                DateTimeOffset.UtcNow,
                loadResult.Dataset.Towers.Count,
                loadResult.Dataset.FibreRanges.Count,
                loadResult.Warnings.Count,
                [.. loadResult.Warnings.Select(issue => issue.Message)]);
            await CommitManifestAsync(manifest, manifestFile, cancellationToken);
        }

        return new WorkbookActivationResult(
            loadResult with
            {
                Dataset = loadResult.Dataset with { SourceFileName = manifest.SourceFileName },
            },
            manifest);
    }

    private static async Task<bool> ManifestMatchesAsync(
        CoverageWorkbookManifest manifest,
        string workbookFile,
        CoverageWorkbookLoadResult loadResult,
        CancellationToken cancellationToken)
    {
        if (manifest.SchemaVersion != CoverageWorkbookManifest.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(manifest.SourceFileName) ||
            manifest.TowerCount != loadResult.Dataset!.Towers.Count ||
            manifest.FibreRangeCount != loadResult.Dataset.FibreRanges.Count ||
            manifest.WarningCount != loadResult.Warnings.Count)
        {
            return false;
        }

        var hash = await ComputeSha256Async(workbookFile, cancellationToken);
        return string.Equals(hash, manifest.Sha256Hex, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<CoverageWorkbookManifest?> TryReadManifestAsync(
        string manifestFile,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(manifestFile))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(manifestFile, cancellationToken);
            return JsonSerializer.Deserialize<CoverageWorkbookManifest>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<RawSlotBackup> CreateRawSlotBackupAsync(
        string workbookFile,
        string manifestFile,
        CancellationToken cancellationToken)
    {
        var workbookBackup = File.Exists(workbookFile) ? NewTemporaryFile("backup", ".xlsx") : null;
        var manifestBackup = File.Exists(manifestFile) ? NewTemporaryFile("backup-manifest", ".json") : null;
        if (workbookBackup is not null)
        {
            await CopyReadableSnapshotAsync(workbookFile, workbookBackup, cancellationToken);
        }

        if (manifestBackup is not null)
        {
            await CopyReadableSnapshotAsync(manifestFile, manifestBackup, cancellationToken);
        }

        return new RawSlotBackup(workbookBackup, manifestBackup);
    }

    private async Task RestoreRawSlotAsync(
        RawSlotBackup backup,
        string workbookFile,
        string manifestFile)
    {
        try
        {
            if (backup.WorkbookFile is null)
            {
                DeleteTargetFile(workbookFile);
            }
            else
            {
                await CommitCopyAsync(backup.WorkbookFile, workbookFile, CancellationToken.None);
            }

            if (backup.ManifestFile is null)
            {
                DeleteTargetFile(manifestFile);
            }
            else
            {
                await CommitCopyAsync(backup.ManifestFile, manifestFile, CancellationToken.None);
            }
        }
        catch (IOException)
        {
            // Startup integrity recovery will retry from the previous known-good slot.
        }
        catch (UnauthorizedAccessException)
        {
            // Startup integrity recovery will retry from the previous known-good slot.
        }
    }

    private async Task CommitCopyAsync(string sourceFile, string targetFile, CancellationToken cancellationToken)
    {
        var stagedFile = NewTemporaryFile("commit", Path.GetExtension(targetFile));
        try
        {
            await CopyReadableSnapshotAsync(sourceFile, stagedFile, cancellationToken);
            File.Move(stagedFile, targetFile, overwrite: true);
        }
        finally
        {
            DeleteTemporaryFile(stagedFile);
        }
    }

    private async Task CommitManifestAsync(
        CoverageWorkbookManifest manifest,
        string targetFile,
        CancellationToken cancellationToken)
    {
        var stagedFile = NewTemporaryFile("manifest", ".json");
        try
        {
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(stagedFile, manifestJson, cancellationToken);
            File.Move(stagedFile, targetFile, overwrite: true);
        }
        finally
        {
            DeleteTemporaryFile(stagedFile);
        }
    }

    private void EnsureDirectories()
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(currentWorkbookFile)!);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(currentManifestFile)!);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(previousWorkbookFile)!);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(previousManifestFile)!);
        _ = Directory.CreateDirectory(temporaryDirectory);
    }

    private string NewTemporaryFile(string prefix, string extension) =>
        Path.Combine(temporaryDirectory, $"{prefix}-{Guid.NewGuid():N}{extension}");

    private static async Task CopyReadableSnapshotAsync(
        string sourceFile,
        string destinationFile,
        CancellationToken cancellationToken)
    {
        await using FileStream source = new FileStream(
            sourceFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 81920,
            useAsync: true);
        await using FileStream destination = new FileStream(
            destinationFile,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static WorkbookActivationResult Failure(string message) =>
        new(
            new CoverageWorkbookLoadResult(
                null,
                [new WorkbookValidationIssue(
                    WorkbookValidationCode.FileReadFailed,
                    WorkbookValidationSeverity.Error,
                    message)]),
            null);

    private static void DeleteTargetFile(string file)
    {
        if (File.Exists(file))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
    }

    private static void DeleteTemporaryFile(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }
        catch (IOException)
        {
            // Startup cleanup will retry abandoned temporary files.
        }
        catch (UnauthorizedAccessException)
        {
            // Startup cleanup will retry abandoned temporary files.
        }
    }

    private sealed record RawSlotBackup(string? WorkbookFile, string? ManifestFile)
    {
        public void Delete()
        {
            if (WorkbookFile is not null)
            {
                DeleteTemporaryFile(WorkbookFile);
            }

            if (ManifestFile is not null)
            {
                DeleteTemporaryFile(ManifestFile);
            }
        }
    }
}
