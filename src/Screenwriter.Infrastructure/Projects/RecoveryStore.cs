using System.IO.Compression;
using System.Text.Json;
using Screenwriter.Core.Projects;

namespace Screenwriter.Infrastructure.Projects;

public sealed class RecoveryStore
{
    private static readonly byte[] Magic = "DRFTRCV1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
    private readonly string _recoveryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Draftroom", "Recovery", "latest.recovery");

    public bool HasRecovery => File.Exists(_recoveryPath);

    public async Task SaveAsync(ScreenplayProject project, string? sourcePath, Guid? activeEpisodeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var directory = Path.GetDirectoryName(_recoveryPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _recoveryPath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await file.WriteAsync(Magic, cancellationToken);
                await using var compressed = new GZipStream(file, CompressionLevel.Fastest, leaveOpen: true);
                await JsonSerializer.SerializeAsync(compressed, new RecoverySnapshot(project, sourcePath, activeEpisodeId, DateTimeOffset.UtcNow), JsonOptions, cancellationToken);
            }

            File.Move(temporaryPath, _recoveryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task<RecoverySnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_recoveryPath))
            return null;

        await using var file = new FileStream(_recoveryPath, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var header = new byte[Magic.Length];
        await file.ReadExactlyAsync(header, cancellationToken);
        if (!header.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("The recovery file is not valid.");

        await using var compressed = new GZipStream(file, CompressionMode.Decompress, leaveOpen: true);
        return await JsonSerializer.DeserializeAsync<RecoverySnapshot>(compressed, JsonOptions, cancellationToken);
    }

    public void Clear()
    {
        if (File.Exists(_recoveryPath))
            File.Delete(_recoveryPath);
    }
}

public sealed record RecoverySnapshot(ScreenplayProject Project, string? SourcePath, Guid? ActiveEpisodeId, DateTimeOffset SavedAt);
