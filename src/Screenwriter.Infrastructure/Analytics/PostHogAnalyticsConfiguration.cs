using System.Text.Json;

namespace Screenwriter.Infrastructure.Analytics;

public sealed class PostHogAnalyticsConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    private PostHogAnalyticsConfiguration(string projectToken, string region)
    {
        ProjectToken = projectToken;
        Region = region;
        Endpoint = region switch
        {
            "us" => new Uri("https://us.i.posthog.com/i/v0/e/"),
            "eu" => new Uri("https://eu.i.posthog.com/i/v0/e/"),
            _ => null
        };
    }

    public string ProjectToken { get; }
    public string Region { get; }
    public Uri? Endpoint { get; }
    public bool IsConfigured => Endpoint is not null && ProjectToken.StartsWith("phc_", StringComparison.Ordinal);

    public static PostHogAnalyticsConfiguration Load()
    {
        var fileConfiguration = ReadFileConfiguration();
        var token = Environment.GetEnvironmentVariable("DRAFTROOM_POSTHOG_PROJECT_TOKEN") ?? fileConfiguration.ProjectToken ?? string.Empty;
        var region = Environment.GetEnvironmentVariable("DRAFTROOM_POSTHOG_REGION") ?? fileConfiguration.Region ?? string.Empty;
        return new PostHogAnalyticsConfiguration(token.Trim(), region.Trim().ToLowerInvariant());
    }

    private static FileConfiguration ReadFileConfiguration()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "posthog-analytics.json");
            if (!File.Exists(path))
                return new FileConfiguration();
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<FileConfiguration>(stream, JsonOptions) ?? new FileConfiguration();
        }
        catch (IOException)
        {
            return new FileConfiguration();
        }
        catch (JsonException)
        {
            return new FileConfiguration();
        }
        catch (UnauthorizedAccessException)
        {
            return new FileConfiguration();
        }
    }

    private sealed class FileConfiguration
    {
        public string? ProjectToken { get; init; }
        public string? Region { get; init; }
    }
}
