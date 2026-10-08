using System.Text.Json;

namespace Screenwriter.Infrastructure.Settings;

public sealed class LocalPreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Draftroom",
        "settings.json");

    public bool LoadDarkTheme()
    {
        return LoadPreferences().DarkTheme;
    }

    public void SaveDarkTheme(bool darkTheme)
    {
        Save(LoadPreferences() with { DarkTheme = darkTheme });
    }

    public string? LoadDriveFolderId()
    {
        return LoadPreferences().DriveFolderId;
    }

    public void SaveDriveFolderId(string? folderId)
    {
        Save(LoadPreferences() with { DriveFolderId = folderId });
    }

    public AnalyticsConsent LoadAnalyticsConsent()
    {
        var preferences = LoadPreferences();
        return new AnalyticsConsent(preferences.AnalyticsChoiceMade, preferences.AnalyticsEnabled);
    }

    public void SaveAnalyticsConsent(bool choiceMade, bool enabled)
    {
        Save(LoadPreferences() with { AnalyticsChoiceMade = choiceMade, AnalyticsEnabled = enabled });
    }

    private static Preferences LoadPreferences()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new Preferences(false);
            using var stream = File.OpenRead(SettingsPath);
            return JsonSerializer.Deserialize<Preferences>(stream, JsonOptions) ?? new Preferences(false);
        }
        catch (IOException)
        {
            return new Preferences(false);
        }
        catch (JsonException)
        {
            return new Preferences(false);
        }
        catch (UnauthorizedAccessException)
        {
            return new Preferences(false);
        }
    }

    private static void Save(Preferences preferences)
    {
        var path = SettingsPath;
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, preferences, JsonOptions);

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private sealed record Preferences(bool DarkTheme, string? DriveFolderId = null, bool AnalyticsChoiceMade = false, bool AnalyticsEnabled = false);
}

public sealed record AnalyticsConsent(bool ChoiceMade, bool Enabled);
