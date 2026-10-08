using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screenwriter.Infrastructure.GoogleDrive;

public sealed class GoogleDriveApiClient(HttpClient? httpClient = null)
{
    private static readonly Uri ApiRoot = new("https://www.googleapis.com/drive/v3/");
    private static readonly Uri UploadRoot = new("https://www.googleapis.com/upload/drive/v3/");
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    private const string ProjectMimeType = "application/vnd.draftroom.project";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    public async Task<string> EnsureDraftroomFolderAsync(string accessToken, string? knownFolderId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(knownFolderId))
        {
            using var knownResponse = await SendAsync(HttpMethod.Get, $"files/{Uri.EscapeDataString(knownFolderId)}?fields=id,mimeType,trashed", accessToken, cancellationToken);
            if (knownResponse.IsSuccessStatusCode)
            {
                var known = await knownResponse.Content.ReadFromJsonAsync<DriveFile>(cancellationToken: cancellationToken);
                if (known is { MimeType: FolderMimeType, Trashed: false })
                    return known.Id;
            }
            else if (knownResponse.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureSuccessAsync(knownResponse, cancellationToken);
            }
        }

        var query = Uri.EscapeDataString("name = 'Draftroom' and mimeType = 'application/vnd.google-apps.folder' and trashed = false");
        using var listResponse = await SendAsync(HttpMethod.Get, $"files?q={query}&pageSize=20&fields=files(id,name,mimeType,trashed)", accessToken, cancellationToken);
        await EnsureSuccessAsync(listResponse, cancellationToken);
        var folders = await listResponse.Content.ReadFromJsonAsync<DriveFileList>(cancellationToken: cancellationToken);
        var folder = folders?.Files.FirstOrDefault(file => file.MimeType == FolderMimeType && !file.Trashed);
        if (folder is not null)
            return folder.Id;

        using var createResponse = await SendJsonAsync(HttpMethod.Post, "files?fields=id,name,mimeType", new
        {
            name = "Draftroom",
            mimeType = FolderMimeType
        }, accessToken, cancellationToken);
        await EnsureSuccessAsync(createResponse, cancellationToken);
        var created = await createResponse.Content.ReadFromJsonAsync<DriveFile>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Google Drive did not return the new Draftroom folder.");
        return created.Id;
    }

    public async Task<DriveFileMetadata> UploadProjectAsync(string accessToken, string filePath, string folderId, CancellationToken cancellationToken = default)
    {
        var boundary = "draftroom_" + Guid.NewGuid().ToString("N");
        using var multipart = new MultipartContent("related", boundary);
        var metadata = new StringContent(JsonSerializer.Serialize(new
        {
            name = Path.GetFileName(filePath),
            mimeType = ProjectMimeType,
            parents = new[] { folderId }
        }), Encoding.UTF8, "application/json");
        multipart.Add(metadata);
        await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(content);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(UploadRoot, "files?uploadType=multipart&fields=id,name,mimeType,headRevisionId,version"))
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ToMetadataAsync(response, cancellationToken);
    }

    public async Task<DriveFileMetadata> GetFileAsync(string accessToken, string fileId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get,
            $"files/{Uri.EscapeDataString(fileId)}?fields=id,name,mimeType,headRevisionId,version,trashed", accessToken, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ToMetadataAsync(response, cancellationToken);
    }

    public async Task<DriveFileMetadata> UpdateProjectAsync(string accessToken, string fileId, string expectedETag, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectedETag))
            throw new InvalidOperationException("Google Drive did not return an ETag. The project was not updated to protect the remote copy.");
        await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            new Uri(UploadRoot, $"files/{Uri.EscapeDataString(fileId)}?uploadType=media&fields=id,name,mimeType,headRevisionId,version"))
        {
            Content = new StreamContent(file)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("If-Match", expectedETag);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            throw new GoogleDriveConflictException("The Drive copy changed during sync.");
        await EnsureSuccessAsync(response, cancellationToken);
        return await ToMetadataAsync(response, cancellationToken);
    }

    public async Task DownloadFileAsync(string accessToken, string fileId, Stream destination, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(ApiRoot, $"files/{Uri.EscapeDataString(fileId)}?alt=media"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(ApiRoot, relativeUri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string relativeUri, object body, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(ApiRoot, relativeUri))
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static async Task<DriveFileMetadata> ToMetadataAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var file = await response.Content.ReadFromJsonAsync<DriveFile>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Google Drive returned incomplete file details.");
        return new DriveFileMetadata(file.Id, file.Name, file.MimeType, file.HeadRevisionId, file.Version, response.Headers.ETag?.ToString(), file.Trashed);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new FileNotFoundException("The selected Google Drive project could not be found or accessed.");
        throw new InvalidOperationException($"Google Drive request failed ({(int)response.StatusCode}). Check the connection and try again.");
    }
}

public sealed record DriveFileMetadata(string Id, string Name, string MimeType, string? HeadRevisionId, string? Version, string? ETag, bool Trashed);
public sealed class GoogleDriveConflictException(string message) : Exception(message);

internal sealed class DriveFile
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("mimeType")] public string MimeType { get; init; } = string.Empty;
    [JsonPropertyName("headRevisionId")] public string? HeadRevisionId { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("trashed")] public bool Trashed { get; init; }
}

internal sealed class DriveFileList
{
    [JsonPropertyName("files")] public List<DriveFile> Files { get; init; } = [];
}
