using System.Text.Json;

namespace Screenwriter.App.Configuration;

public sealed class GoogleDriveOAuthConfiguration
{
    public string ClientId { get; init; } = string.Empty;

    public bool IsConfigured => ClientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase);

    public static GoogleDriveOAuthConfiguration Load()
    {
        var clientId = Environment.GetEnvironmentVariable("DRAFTROOM_GOOGLE_DRIVE_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(clientId))
            return new GoogleDriveOAuthConfiguration { ClientId = clientId.Trim() };

        var path = Path.Combine(AppContext.BaseDirectory, "google-drive-oauth.json");
        if (!File.Exists(path))
            return new GoogleDriveOAuthConfiguration();

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<GoogleDriveOAuthConfiguration>(stream) ?? new GoogleDriveOAuthConfiguration();
        }
        catch (IOException)
        {
            return new GoogleDriveOAuthConfiguration();
        }
        catch (JsonException)
        {
            return new GoogleDriveOAuthConfiguration();
        }
    }
}
