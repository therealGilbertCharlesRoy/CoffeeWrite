namespace Screenwriter.App.ViewModels;

public sealed record SpellingIssue(string Word, int Offset, IReadOnlyList<string> Suggestions);
