using Screenwriter.Core.Documents;

namespace Screenwriter.Core.Formatting;

public enum ScriptLayoutKind
{
    Screenplay,
    Television,
    TwoColumnAudioVisual,
    StagePlay
}

public sealed record ScriptElementRule(
    ScriptElementType Type,
    string Label,
    bool StartsNewLine = true,
    bool StartsNewPage = false,
    bool Uppercase = false,
    double LeftIndentInches = 0,
    double RightIndentInches = 0,
    double SpaceBeforePoints = 0,
    double SpaceAfterPoints = 0);

public sealed class ScriptFormatProfile
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required ScriptLayoutKind Layout { get; init; }
    public required IReadOnlyList<ScriptElementRule> Elements { get; init; }
    public IReadOnlyList<ScriptElementType> DefaultElementSequence { get; init; } = [];
    public bool SupportsDualDialogue { get; init; }
    public bool SupportsActHeadings { get; init; }
    public bool SupportsSceneNumbers { get; init; }
    public bool HasTitlePage { get; init; } = true;
    public ScriptElementRule? FindRule(ScriptElementType type) => Elements.FirstOrDefault(rule => rule.Type == type);
}

public static class ScriptFormatProfiles
{
    private static readonly ScriptElementRule[] ScreenplayElements =
    [
        new(ScriptElementType.SceneHeading, "Scene heading", Uppercase: true, SpaceBeforePoints: 18, SpaceAfterPoints: 12),
        new(ScriptElementType.Action, "Action", SpaceAfterPoints: 12),
        new(ScriptElementType.Character, "Character", Uppercase: true, LeftIndentInches: 2.2, SpaceBeforePoints: 12),
        new(ScriptElementType.Dialogue, "Dialogue", LeftIndentInches: 1.0, RightIndentInches: 1.5),
        new(ScriptElementType.Parenthetical, "Parenthetical", LeftIndentInches: 1.6, RightIndentInches: 2.0),
        new(ScriptElementType.Transition, "Transition", Uppercase: true, RightIndentInches: 0, SpaceBeforePoints: 12),
        new(ScriptElementType.Shot, "Shot", Uppercase: true, SpaceBeforePoints: 12)
    ];

    private static readonly ScriptElementType[] ScreenplaySequence =
    [
        ScriptElementType.SceneHeading, ScriptElementType.Action,
        ScriptElementType.Character, ScriptElementType.Dialogue, ScriptElementType.Parenthetical
    ];

    public static IReadOnlyList<ScriptFormatProfile> All { get; } =
    [
        new ScriptFormatProfile
        {
            Id = "movie", DisplayName = "Movie", Description = "Feature screenplay format.",
            Layout = ScriptLayoutKind.Screenplay, Elements = ScreenplayElements,
            DefaultElementSequence = ScreenplaySequence, SupportsDualDialogue = true,
            SupportsSceneNumbers = true
        },
        new ScriptFormatProfile
        {
            Id = "short-film", DisplayName = "Short Film", Description = "Screenplay format for short films.",
            Layout = ScriptLayoutKind.Screenplay, Elements = ScreenplayElements,
            DefaultElementSequence = ScreenplaySequence, SupportsDualDialogue = true,
            SupportsSceneNumbers = true
        },
        new ScriptFormatProfile
        {
            Id = "tv", DisplayName = "TV Episode", Description = "Teleplay format with acts and scene numbering.",
            Layout = ScriptLayoutKind.Television,
            Elements = [.. ScreenplayElements, new(ScriptElementType.ActHeading, "Act heading", Uppercase: true, StartsNewPage: true, SpaceBeforePoints: 18, SpaceAfterPoints: 18)],
            DefaultElementSequence = [ScriptElementType.ActHeading, .. ScreenplaySequence],
            SupportsDualDialogue = true, SupportsActHeadings = true, SupportsSceneNumbers = true
        },
        new ScriptFormatProfile
        {
            Id = "youtube", DisplayName = "YouTube Video", Description = "Two-column audio and visual production script.",
            Layout = ScriptLayoutKind.TwoColumnAudioVisual,
            Elements =
            [
                new(ScriptElementType.SectionHeading, "Section", Uppercase: true, SpaceBeforePoints: 12, SpaceAfterPoints: 8),
                new(ScriptElementType.Audio, "Audio", LeftIndentInches: 0.25, RightIndentInches: 4.0),
                new(ScriptElementType.Visual, "Visual", LeftIndentInches: 4.25, RightIndentInches: 0.25),
                new(ScriptElementType.Action, "Direction", SpaceAfterPoints: 8)
            ],
            DefaultElementSequence = [ScriptElementType.SectionHeading, ScriptElementType.Audio, ScriptElementType.Visual],
            HasTitlePage = false
        },
        new ScriptFormatProfile
        {
            Id = "theatre", DisplayName = "Theatre Play", Description = "Stage-play format with acts, scenes, and stage directions.",
            Layout = ScriptLayoutKind.StagePlay,
            Elements =
            [
                new(ScriptElementType.ActHeading, "Act", Uppercase: true, StartsNewPage: true, SpaceBeforePoints: 18, SpaceAfterPoints: 18),
                new(ScriptElementType.SceneHeading, "Scene", Uppercase: true, SpaceBeforePoints: 14, SpaceAfterPoints: 12),
                new(ScriptElementType.StageDirection, "Stage direction", SpaceAfterPoints: 12),
                new(ScriptElementType.Character, "Character", Uppercase: true, LeftIndentInches: 2.0, SpaceBeforePoints: 10),
                new(ScriptElementType.Dialogue, "Dialogue", LeftIndentInches: 0.75, RightIndentInches: 1.0),
                new(ScriptElementType.Parenthetical, "Parenthetical", LeftIndentInches: 1.5, RightIndentInches: 1.5),
                new(ScriptElementType.Lyrics, "Lyrics", LeftIndentInches: 1.0, RightIndentInches: 1.0)
            ],
            DefaultElementSequence = [ScriptElementType.ActHeading, ScriptElementType.SceneHeading, ScriptElementType.StageDirection, ScriptElementType.Character, ScriptElementType.Dialogue]
        }
    ];

    public static ScriptFormatProfile Get(string id) => All.FirstOrDefault(profile => profile.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown script format profile '{id}'.", nameof(id));
}
