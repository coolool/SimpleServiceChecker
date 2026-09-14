using System.Security.Cryptography;
using System.Text.Json;
using SimpleServiceChecker.Core.Coverage;
using SimpleServiceChecker.Core.Workbooks;

namespace SimpleServiceChecker.App.Infrastructure;

public sealed record WorkbookActivationResult(
    CoverageWorkbookLoadResult LoadResult,
    CoverageWorkbookManifest? Manifest);

public sealed class WorkbookActivationService(
    string currentWorkbookFile,
    string currentManifestFile,
    string temporaryDirectory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string currentWorkbookFile = currentWorkbookFile;
    private readonly string currentManifestFile = currentManifestFile;
    private readonly string temporaryDirectory = temporaryDirectory;

    public async Task<WorkbookActivationResult> ActivateAsync(
        string sourceFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(currentWorkbookFile)!);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(currentManifestFile)!);
        _ = Directory.CreateDirectory(temporaryDirectory);

        var temporaryWorkbook = Path.Combine(temporaryDirectory, $"coverage-{Guid.NewGuid():N}.xlsx");
        try
        {
            await CopyReadableSnapshotAsync(sourceFile, temporaryWorkbook, cancellationToken);
            var loadResult = await CoverageWorkbookReader.LoadAsync(temporaryWorkbook, cancellationToken);
            if (!loadResult.IsValid || loadResult.Dataset is null)
            {
                return new WorkbookActivationResult(loadResult, null);
            }

            var hash = await ComputeSha256Async(temporaryWorkbook, cancellationToken);
            CoverageWorkbookManifest manifest = new CoverageWorkbookManifest(
                CoverageWorkbookManifest.CurrentSchemaVersion,
                Path.GetFileName(sourceFile),
                hash,
                DateTimeOffset.UtcNow,
                loadResult.Dataset.Towers.Count,
                loadResult.Dataset.FibreRanges.Count,
                loadResult.Warnings.Count);

            File.Move(temporaryWorkbook, currentWorkbookFile, overwrite: true);
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(currentManifestFile, manifestJson, cancellationToken);

            return new WorkbookActivationResult(
                loadResult with
                {
                    Dataset = loadResult.Dataset with { SourceFileName = manifest.SourceFileName },
                },
                manifest);
        }
        finally
        {
            if (File.Exists(temporaryWorkbook))
            {
                File.Delete(temporaryWorkbook);
            }
        }
    }

    public async Task<WorkbookActivationResult?> LoadCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(currentWorkbookFile))
        {
            return null;
        }

        var loadResult = await CoverageWorkbookReader.LoadAsync(currentWorkbookFile, cancellationToken);
        if (!loadResult.IsValid || loadResult.Dataset is null)
        {
            return new WorkbookActivationResult(loadResult, null);
        }

        CoverageWorkbookManifest? manifest = null;
        try
        {
            if (File.Exists(currentManifestFile))
            {
                var json = await File.ReadAllTextAsync(currentManifestFile, cancellationToken);
                manifest = JsonSerializer.Deserialize<CoverageWorkbookManifest>(json, JsonOptions);
            }
        }
        catch (JsonException)
        {
            // The workbook remains usable even if its display-only manifest is damaged.
        }

        return new WorkbookActivationResult(
            manifest is null
                ? loadResult
                : loadResult with
                {
                    Dataset = loadResult.Dataset with { SourceFileName = manifest.SourceFileName },
                },
            manifest);
    }

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
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
