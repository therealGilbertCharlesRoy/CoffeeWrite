using System.IO.Compression;
using System.Text.Json;
using Screenwriter.Core.Projects;

namespace Screenwriter.Infrastructure.Projects;

public sealed class LocalProjectStore
{
    private static readonly byte[] Magic = "DRFTROOM"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false
    };

    public async Task SaveAsync(string path, ScreenplayProject project, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new IOException("The selected project path has no parent folder.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await file.WriteAsync(Magic, cancellationToken);
                await file.WriteAsync(BitConverter.GetBytes(project.FormatVersion), cancellationToken);
                await using var compressed = new GZipStream(file, CompressionLevel.Optimal, leaveOpen: true);
                await JsonSerializer.SerializeAsync(compressed, project, JsonOptions, cancellationToken);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task<ScreenplayProject> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var file = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var header = new byte[Magic.Length + sizeof(int)];
        await file.ReadExactlyAsync(header, cancellationToken);

        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("This file is not a Draftroom project.");

        var containerVersion = BitConverter.ToInt32(header, Magic.Length);
        if (containerVersion != 1)
            throw new NotSupportedException($"Project format version {containerVersion} is not supported by this version of Draftroom.");

        await using var compressed = new GZipStream(file, CompressionMode.Decompress, leaveOpen: true);
        var project = await JsonSerializer.DeserializeAsync<ScreenplayProject>(compressed, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The project file is empty or damaged.");

        if (project.FormatVersion != containerVersion)
            throw new InvalidDataException("The project header and project data versions do not match.");

        return project;
    }
}
