using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screenwriter.Infrastructure.GoogleDrive;

public sealed class GoogleDriveOAuthClient(HttpClient? httpClient = null)
{
    private static readonly Uri AuthorizationEndpoint = new("https://accounts.google.com/o/oauth2/v2/auth");
    private static readonly Uri TokenEndpoint = new("https://oauth2.googleapis.com/token");
    private const string DriveFileScope = "https://www.googleapis.com/auth/drive.file";
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<GoogleDriveTokens> AuthorizeAsync(string clientId, Func<Uri, Task> openBrowser, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || !clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A valid Google desktop OAuth client ID is required.");

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirectUri = new Uri($"http://127.0.0.1:{port}/");
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri.AbsoluteUri,
            ["response_type"] = "code",
            ["scope"] = DriveFileScope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };
        var authorizationUri = new UriBuilder(AuthorizationEndpoint) { Query = BuildQuery(query) }.Uri;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        await openBrowser(authorizationUri);

        using var callback = await listener.AcceptTcpClientAsync(timeout.Token);
        if (callback.Client.RemoteEndPoint is not IPEndPoint remote || !IPAddress.IsLoopback(remote.Address))
            throw new InvalidOperationException("The authorization response did not come from this device.");

        var (code, returnedState, oauthError) = await ReadCallbackAsync(callback, timeout.Token);
        if (oauthError is not null)
            throw new InvalidOperationException($"Google authorization was not completed: {oauthError}");
        if (string.IsNullOrEmpty(code) || !FixedEquals(state, returnedState))
            throw new InvalidOperationException("Google authorization returned an invalid response.");

        using var response = await _httpClient.PostAsync(TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = redirectUri.AbsoluteUri,
                ["grant_type"] = "authorization_code"
            }), timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Google token exchange failed. Check the OAuth client configuration and try again.");
        var tokens = JsonSerializer.Deserialize<GoogleDriveTokens>(body)
            ?? throw new InvalidOperationException("Google returned an unreadable token response.");
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
            throw new InvalidOperationException("Google did not provide a refresh token. Disconnect the app in your Google Account settings and connect again.");
        return tokens;
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(new Uri("https://oauth2.googleapis.com/revoke"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token }), cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
            throw new InvalidOperationException("Google could not revoke Drive access. Try again while online.");
    }

    public async Task<GoogleDriveTokens> RefreshAccessTokenAsync(string clientId, string refreshToken, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            }), cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Google Drive access expired or was revoked. Connect the account again.");
        return await response.Content.ReadFromJsonAsync<GoogleDriveTokens>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Google returned an unreadable access-token response.");
    }

    private static async Task<(string? Code, string? State, string? Error)> ReadCallbackAsync(TcpClient callback, CancellationToken cancellationToken)
    {
        await using var stream = callback.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken);
        if (requestLine is null || !requestLine.StartsWith("GET ", StringComparison.Ordinal))
            throw new InvalidOperationException("Google returned an invalid authorization response.");
        var path = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken))) { }
        var callbackUri = new Uri(new Uri("http://127.0.0.1/"), path ?? "/");
        var values = ParseQuery(callbackUri.Query);
        var html = "<html><body><p>Draftroom Drive authorization is complete. You can return to Draftroom.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return (values.GetValueOrDefault("code"), values.GetValueOrDefault("state"), values.GetValueOrDefault("error"));
    }

    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Where(pair => pair.Length == 2)
        .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')), StringComparer.Ordinal);

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> values) => string.Join("&", values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool FixedEquals(string expected, string? actual)
    {
        if (actual is null)
            return false;
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public sealed class GoogleDriveTokens
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; init; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresInSeconds { get; init; }
    [JsonPropertyName("token_type")] public string TokenType { get; init; } = string.Empty;
}
