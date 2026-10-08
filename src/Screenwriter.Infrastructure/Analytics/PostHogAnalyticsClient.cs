using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screenwriter.Infrastructure.Analytics;

public enum ProductAnalyticsEvent
{
    AppLaunched,
    FeatureUsed,
    DriveConnected,
    DriveConnectionFailed,
    DriveDisconnected,
    DriveProjectUploaded,
    DriveSyncCompleted,
    DriveSyncConflict,
    DriveSyncFailed
}

public enum AnalyticsFeature
{
    BeatBoard,
    Editor,
    AiPlaceholder,
    CharacterTracker,
    RelationshipTracker,
    GoogleDrive,
    PdfExport,
    FountainImport,
    ThemeSelection
}

public sealed class PostHogAnalyticsClient
{
    private const int MaximumQueuedEvents = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Draftroom", "Analytics");
    private static readonly string QueuePath = Path.Combine(DataDirectory, "events.json");
    private static readonly string InstallationIdPath = Path.Combine(DataDirectory, "installation-id");
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly PostHogAnalyticsConfiguration _configuration = PostHogAnalyticsConfiguration.Load();
    private bool _consentEnabled;
    private bool _dataClearPending;
    private CancellationTokenSource _consentCancellation = new();

    public bool IsConfigured => _configuration.IsConfigured;

    public string ConfigurationStatus => IsConfigured
        ? $"PostHog {(_configuration.Region == "eu" ? "EU" : "US")} Cloud is configured. Network requests reveal your IP address; disable IP data capture in PostHog project settings. Events are sent only after opt-in."
        : "PostHog is not configured in this build, so no analytics data is sent.";

    public void SetConsent(bool enabled)
    {
        lock (_stateLock)
        {
            if (!enabled)
            {
                _consentEnabled = false;
                _consentCancellation.Cancel();
                _consentCancellation.Dispose();
                _consentCancellation = new CancellationTokenSource();
                ClearLocalData();
                return;
            }

            if (_dataClearPending)
                ClearLocalData();
            _consentEnabled = !_dataClearPending;
        }
    }

    public void Track(ProductAnalyticsEvent eventName, AnalyticsFeature? feature = null)
    {
        CancellationToken cancellationToken;
        lock (_stateLock)
        {
            if (!_consentEnabled || !_configuration.IsConfigured)
                return;

            try
            {
                var events = ReadQueue();
                var envelope = new AnalyticsEventEnvelope(
                    Guid.NewGuid().ToString("N"),
                    EventName(eventName),
                    DateTimeOffset.UtcNow,
                    InstallationId(),
                    BuildProperties(feature));
                events.Add(envelope);
                if (events.Count > MaximumQueuedEvents)
                    events.RemoveRange(0, events.Count - MaximumQueuedEvents);
                WriteQueue(events);
                cancellationToken = _consentCancellation.Token;
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }

        _ = FlushPendingAsync(cancellationToken);
    }

    public Task FlushPendingAsync()
    {
        lock (_stateLock)
        {
            if (!_consentEnabled || !_configuration.IsConfigured)
                return Task.CompletedTask;
            return FlushPendingAsync(_consentCancellation.Token);
        }
    }

    private async Task FlushPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _flushGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                AnalyticsEventEnvelope? next;
                lock (_stateLock)
                {
                    if (!_consentEnabled)
                        return;
                    next = ReadQueue().FirstOrDefault();
                }
                if (next is null)
                    return;

                using var response = await _httpClient.PostAsJsonAsync(_configuration.Endpoint!, new PostHogCapture(
                    _configuration.ProjectToken,
                    next.Event,
                    next.DistinctId,
                    next.Properties,
                    next.Timestamp), JsonOptions, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500
                        && response.StatusCode != HttpStatusCode.TooManyRequests)
                        RemoveQueuedEvent(next.EventId);
                    return;
                }

                RemoveQueuedEvent(next.EventId);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private void RemoveQueuedEvent(string eventId)
    {
        lock (_stateLock)
        {
            if (!_consentEnabled)
                return;
            var events = ReadQueue();
            events.RemoveAll(item => item.EventId == eventId);
            WriteQueue(events);
        }
    }

    private void ClearLocalData()
    {
        try
        {
            if (File.Exists(QueuePath))
                File.Delete(QueuePath);
            if (File.Exists(InstallationIdPath))
                File.Delete(InstallationIdPath);
            _dataClearPending = false;
        }
        catch (IOException)
        {
            _dataClearPending = true;
        }
        catch (UnauthorizedAccessException)
        {
            _dataClearPending = true;
        }
    }

    private static string InstallationId()
    {
        if (File.Exists(InstallationIdPath))
        {
            var existing = File.ReadAllText(InstallationIdPath).Trim();
            if (Guid.TryParseExact(existing, "N", out _))
                return existing;
        }

        Directory.CreateDirectory(DataDirectory);
        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(InstallationIdPath, id);
        return id;
    }

    private static List<AnalyticsEventEnvelope> ReadQueue()
    {
        try
        {
            if (!File.Exists(QueuePath))
                return [];
            using var stream = File.OpenRead(QueuePath);
            return JsonSerializer.Deserialize<List<AnalyticsEventEnvelope>>(stream, JsonOptions) ?? [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void WriteQueue(List<AnalyticsEventEnvelope> events)
    {
        Directory.CreateDirectory(DataDirectory);
        var temporaryPath = Path.Combine(DataDirectory, $"events.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, events, JsonOptions);
            File.Move(temporaryPath, QueuePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static Dictionary<string, object> BuildProperties(AnalyticsFeature? feature)
    {
        var properties = new Dictionary<string, object>
        {
            ["$process_person_profile"] = false,
            ["app_version"] = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            ["platform"] = PlatformName()
        };
        if (feature is { } value)
            properties["feature"] = FeatureName(value);
        return properties;
    }

    private static string EventName(ProductAnalyticsEvent eventName) => eventName switch
    {
        ProductAnalyticsEvent.AppLaunched => "app_launched",
        ProductAnalyticsEvent.FeatureUsed => "feature_used",
        ProductAnalyticsEvent.DriveConnected => "drive_connected",
        ProductAnalyticsEvent.DriveConnectionFailed => "drive_connection_failed",
        ProductAnalyticsEvent.DriveDisconnected => "drive_disconnected",
        ProductAnalyticsEvent.DriveProjectUploaded => "drive_project_uploaded",
        ProductAnalyticsEvent.DriveSyncCompleted => "drive_sync_completed",
        ProductAnalyticsEvent.DriveSyncConflict => "drive_sync_conflict",
        ProductAnalyticsEvent.DriveSyncFailed => "drive_sync_failed",
        _ => throw new ArgumentOutOfRangeException(nameof(eventName))
    };

    private static string FeatureName(AnalyticsFeature feature) => feature switch
    {
        AnalyticsFeature.BeatBoard => "beat_board",
        AnalyticsFeature.Editor => "editor",
        AnalyticsFeature.AiPlaceholder => "ai_placeholder",
        AnalyticsFeature.CharacterTracker => "character_tracker",
        AnalyticsFeature.RelationshipTracker => "relationship_tracker",
        AnalyticsFeature.GoogleDrive => "google_drive",
        AnalyticsFeature.PdfExport => "pdf_export",
        AnalyticsFeature.FountainImport => "fountain_import",
        AnalyticsFeature.ThemeSelection => "theme_selection",
        _ => throw new ArgumentOutOfRangeException(nameof(feature))
    };

    private static string PlatformName() => OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "macos"
        : OperatingSystem.IsLinux() ? "linux"
        : "other";

    private sealed record AnalyticsEventEnvelope(
        string EventId,
        string Event,
        DateTimeOffset Timestamp,
        string DistinctId,
        Dictionary<string, object> Properties);

    private sealed record PostHogCapture(
        [property: JsonPropertyName("api_key")] string ApiKey,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("distinct_id")] string DistinctId,
        [property: JsonPropertyName("properties")] Dictionary<string, object> Properties,
        [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
}
