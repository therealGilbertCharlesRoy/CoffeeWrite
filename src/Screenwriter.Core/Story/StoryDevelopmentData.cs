namespace Screenwriter.Core.Story;

public sealed class StoryDevelopmentData
{
    public List<BeatGroup> BeatGroups { get; set; } = [];
    public List<BeatCard> Beats { get; set; } = [];
    public List<CharacterProfile> Characters { get; set; } = [];
    public List<CharacterRelationship> Relationships { get; set; } = [];
    public RelationshipMapState RelationshipMap { get; set; } = new();
}

public sealed class BeatGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New section";
    public int Order { get; set; }
}

public sealed class BeatCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? GroupId { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = "New beat";
    public string Notes { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Color { get; set; }
    public SceneReference? SceneLink { get; set; }
}

public sealed class SceneReference
{
    public Guid ScriptDocumentId { get; set; }
    public Guid? SceneId { get; set; }
    public string? AnchorText { get; set; }
}

public sealed class CharacterProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New character";
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Role"] = string.Empty,
        ["Description"] = string.Empty,
        ["Goals"] = string.Empty,
        ["Arc"] = string.Empty
    };
    public double MapX { get; set; }
    public double MapY { get; set; }
}

public enum RelationshipDirection
{
    Mutual,
    FromFirstToSecond,
    FromSecondToFirst
}

public sealed class CharacterRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirstCharacterId { get; set; }
    public Guid SecondCharacterId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public RelationshipDirection Direction { get; set; } = RelationshipDirection.Mutual;
}

public sealed class RelationshipMapState
{
    public double Zoom { get; set; } = 1;
    public double PanX { get; set; }
    public double PanY { get; set; }
}
