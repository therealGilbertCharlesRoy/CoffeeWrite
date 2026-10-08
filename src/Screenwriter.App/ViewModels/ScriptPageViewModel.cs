using Screenwriter.Core.Documents;
using Avalonia;

namespace Screenwriter.App.ViewModels;

public sealed record ScriptPreviewRowViewModel(
    string LeftText,
    string RightText,
    string FullText,
    bool IsFullWidth,
    Thickness Margin);

public sealed record ScriptPageViewModel(
    int Index,
    int StartOffset,
    int PageNumber,
    bool IsTitlePage,
    bool IsScriptPage,
    bool ShowPageNumber,
    string Title,
    string Subtitle,
    string Author,
    string DraftDate,
    string Contact,
    bool IsTwoColumn,
    IReadOnlyList<ScriptPreviewRowViewModel> Rows,
    double PageHeight,
    Thickness PagePadding);
