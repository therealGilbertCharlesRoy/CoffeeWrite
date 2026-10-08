using Screenwriter.Core.Documents;
using Screenwriter.Core.Formatting;

namespace Screenwriter.Editor;

public static class ScriptEditingController
{
    public static ScriptElementType GetElementTypeAtLine(ScriptDocument document, int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= document.Elements.Count)
            return ScriptElementType.Action;
        return document.Elements[lineIndex].Type;
    }

    public static void SetElementTypeAtLine(ScriptDocument document, string text, int lineIndex, ScriptElementType type)
    {
        SyncElements(document, text);
        if (lineIndex < 0 || lineIndex >= document.Elements.Count)
            return;
        document.Elements[lineIndex].Type = type;
    }

    public static void SyncElements(ScriptDocument document, string text)
    {
        var prior = document.Elements;
        var usedIds = new HashSet<Guid>();
        var lines = text.Split('\n');
        var next = new List<ScriptElement>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var inferredType = InferType(line);
            var existing = prior.FirstOrDefault(element => !usedIds.Contains(element.Id)
                && element.Type == inferredType
                && element.Text.Equals(line, StringComparison.Ordinal));
            if (existing is null && i < prior.Count && !usedIds.Contains(prior[i].Id) && prior[i].Type == inferredType)
                existing = prior[i];
            var id = existing?.Id ?? Guid.NewGuid();
            usedIds.Add(id);
            next.Add(new ScriptElement
            {
                Id = id,
                Type = existing?.Type ?? InferType(line),
                Text = line,
                CharacterName = existing?.CharacterName,
                SceneNumber = existing?.SceneNumber,
                IsLockedToPage = existing?.IsLockedToPage ?? false,
                RevisionId = existing?.RevisionId
            });
        }
        document.Elements = next;
        document.Text = text;
    }

    public static ScriptElementType GetNextElementType(ScriptFormatProfile profile, ScriptElementType current, bool reverse = false)
    {
        var sequence = profile.DefaultElementSequence;
        if (sequence.Count == 0)
            return current;
        var index = -1;
        for (var i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] == current)
            {
                index = i;
                break;
            }
        }
        var next = index < 0 ? (reverse ? sequence.Count - 1 : 0) : (index + (reverse ? -1 : 1) + sequence.Count) % sequence.Count;
        return sequence[next];
    }

    public static ScriptElementType GetElementAfterEnter(ScriptElementType current) => current switch
    {
        ScriptElementType.Character => ScriptElementType.Dialogue,
        ScriptElementType.Dialogue or ScriptElementType.Parenthetical => ScriptElementType.Action,
        ScriptElementType.SceneHeading => ScriptElementType.Action,
        _ => current
    };

    private static ScriptElementType InferType(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("INT.", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("EXT.", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("INT/EXT.", StringComparison.OrdinalIgnoreCase))
            return ScriptElementType.SceneHeading;
        if (trimmed.EndsWith(" TO:", StringComparison.OrdinalIgnoreCase))
            return ScriptElementType.Transition;
        if (trimmed.Length is > 0 and < 38 && trimmed == trimmed.ToUpperInvariant()
            && trimmed.All(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character is '(' or ')' or '\''))
            return ScriptElementType.Character;
        return ScriptElementType.Action;
    }
}
