namespace Screenwriter.Core.Documents;

public enum ScriptElementType
{
    SceneHeading,
    Action,
    Character,
    Dialogue,
    Parenthetical,
    Transition,
    Shot,
    ActHeading,
    DualDialogue,
    StageDirection,
    Lyrics,
    Audio,
    Visual,
    SectionHeading
}

public sealed class ScriptDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled screenplay";
    public string FormatProfile { get; set; } = "movie";
    public string Text { get; set; } = string.Empty;
    public List<ScriptElement> Elements { get; set; } = [];
    public ScriptPageSettings PageSettings { get; set; } = new();
    public ScriptTitlePage TitlePage { get; set; } = new();
    public List<ScriptRevisionSnapshot> Revisions { get; set; } = [];
}

public sealed class ScriptElement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ScriptElementType Type { get; set; } = ScriptElementType.Action;
    public string Text { get; set; } = string.Empty;
    public string? CharacterName { get; set; }
    public string? SceneNumber { get; set; }
    public bool IsLockedToPage { get; set; }
    public Guid? RevisionId { get; set; }
}

public sealed class ScriptPageSettings
{
    public string PaperSize { get; set; } = "USLetter";
    public double LeftMarginInches { get; set; } = 1.5;
    public double RightMarginInches { get; set; } = 1.0;
    public double TopMarginInches { get; set; } = 1.0;
    public double BottomMarginInches { get; set; } = 1.0;
    public bool ShowPageNumbers { get; set; } = true;
    public int FirstPageNumber { get; set; } = 1;
}

public sealed class ScriptTitlePage
{
    public bool Enabled { get; set; } = true;
    public string Subtitle { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string DraftDate { get; set; } = string.Empty;
}

public sealed class ScriptRevisionSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Draft";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string RevisionColor { get; set; } = "Blue";
    public List<ScriptElement> Elements { get; set; } = [];
    public HashSet<Guid> LockedPageElementIds { get; set; } = [];
}
