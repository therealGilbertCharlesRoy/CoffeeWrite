using ScriptDocument = Screenwriter.Core.Documents.ScriptDocument;
using Screenwriter.Core.Story;

namespace Screenwriter.Core.Projects;

public enum ProjectKind
{
    StandaloneScript,
    TelevisionSeries
}

public sealed class ScreenplayProject
{
    public int FormatVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled screenplay";
    public ProjectKind Kind { get; set; } = ProjectKind.StandaloneScript;
    public ScriptDocument? StandaloneScript { get; set; } = new();
    public SeriesOutline? Series { get; set; }
    public StoryDevelopmentData Story { get; set; } = new();
    public DriveSyncSettings DriveSync { get; set; } = new();
}

public sealed class DriveSyncSettings
{
    public bool SelectedForSync { get; set; }
    public bool AutoSyncEnabled { get; set; }
    public bool PendingSync { get; set; }
    public string? DriveFileId { get; set; }
    public string? DriveFolderId { get; set; }
    public string? RemoteRevisionId { get; set; }
    public string? RemoteETag { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
}

public sealed class SeriesOutline
{
    public string Title { get; set; } = "Untitled series";
    public List<SeriesSeason> Seasons { get; set; } = [];
}

public sealed class SeriesSeason
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; } = 1;
    public string Title { get; set; } = "Season 1";
    public List<SeriesEpisode> Episodes { get; set; } = [];
}

public sealed class SeriesEpisode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; } = 1;
    public string Title { get; set; } = "Episode 1";
    public ScriptDocument Script { get; set; } = new();
}
