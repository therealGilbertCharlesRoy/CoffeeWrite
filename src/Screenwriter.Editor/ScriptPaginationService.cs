using Screenwriter.Core.Documents;
using Screenwriter.Core.Formatting;

namespace Screenwriter.Editor;

public sealed record ScriptPageLayout(
    int Index,
    int StartOffset,
    int Length,
    int PageNumber,
    bool IsTitlePage,
    string Content);

public static class ScriptPaginationService
{
    private const double PointsPerLine = 12;
    private const double CharactersPerInch = 10;

    public static IReadOnlyList<string> WrapLine(
        string text,
        ScriptElementRule? rule,
        ScriptPageSettings settings,
        ScriptLayoutKind layout,
        bool columnWidth = false)
    {
        var (width, _) = GetPaperDimensions(settings.PaperSize);
        var availableWidth = Math.Max(18, width - (settings.LeftMarginInches + settings.RightMarginInches) * 72);
        if (columnWidth)
            availableWidth = Math.Max(18, (availableWidth - 12) / 2);

        var indentPoints = layout == ScriptLayoutKind.TwoColumnAudioVisual
            ? 0
            : ((rule?.LeftIndentInches ?? 0) + (rule?.RightIndentInches ?? 0)) * 72;
        var charactersPerLine = Math.Max(12, (int)Math.Floor(Math.Max(36, availableWidth - indentPoints) / (72 / CharactersPerInch)));
        if (text.Length == 0)
            return [string.Empty];

        var result = new List<string>();
        var remaining = text;
        while (remaining.Length > charactersPerLine)
        {
            var split = remaining.LastIndexOf(' ', charactersPerLine - 1, charactersPerLine);
            if (split <= 0)
                split = charactersPerLine;
            result.Add(remaining[..split].TrimEnd());
            remaining = remaining[split..].TrimStart();
        }
        result.Add(remaining);
        return result;
    }

    public static IReadOnlyList<ScriptPageLayout> Paginate(ScriptDocument document, ScriptFormatProfile profile)
    {
        var pages = new List<ScriptPageLayout>();
        if (profile.HasTitlePage && document.TitlePage.Enabled)
        {
            pages.Add(new ScriptPageLayout(0, 0, 0, 0, true, string.Empty));
        }

        var lines = document.Text.Split('\n');
        var scriptPageNumber = 0;
        var pageStart = 0;
        var usedLines = 0;
        var pageCapacity = GetPageCapacity(document.PageSettings);
        var elements = document.Elements;
        var offset = 0;

        var lineIndex = 0;
        while (lineIndex < lines.Length)
        {
            var line = lines[lineIndex].TrimEnd('\r');
            var type = lineIndex < elements.Count ? elements[lineIndex].Type : ScriptElementType.Action;
            var rule = profile.FindRule(type);
            var isTwoColumn = profile.Layout == ScriptLayoutKind.TwoColumnAudioVisual;
            var columnWidth = isTwoColumn && (type is ScriptElementType.Audio or ScriptElementType.Visual);
            var lineCost = GetLineCost(line, rule, document.PageSettings, profile.Layout, columnWidth);
            var startsNewPage = rule?.StartsNewPage == true;
            var groupEnd = lineIndex;

            if (isTwoColumn && lineIndex + 1 < lines.Length)
            {
                var nextType = lineIndex + 1 < elements.Count ? elements[lineIndex + 1].Type : ScriptElementType.Action;
                var isAudioVisualPair = type == ScriptElementType.Audio && nextType == ScriptElementType.Visual
                    || type == ScriptElementType.Visual && nextType == ScriptElementType.Audio;
                if (isAudioVisualPair)
                {
                    var nextRule = profile.FindRule(nextType);
                    var nextCost = GetLineCost(lines[lineIndex + 1].TrimEnd('\r'), nextRule, document.PageSettings, profile.Layout, true);
                    lineCost = Math.Max(lineCost, nextCost);
                    groupEnd++;
                }
            }

            if (usedLines > 0 && (startsNewPage || usedLines + lineCost > pageCapacity))
            {
                AddScriptPage(pages, document, pageStart, offset - pageStart, ref scriptPageNumber);
                pageStart = offset;
                usedLines = 0;
            }

            usedLines += lineCost;
            for (; lineIndex <= groupEnd; lineIndex++)
                offset += lines[lineIndex].Length + (lineIndex < lines.Length - 1 ? 1 : 0);
        }

        if (pageStart < document.Text.Length || scriptPageNumber == 0)
            AddScriptPage(pages, document, pageStart, document.Text.Length - pageStart, ref scriptPageNumber);

        return pages;
    }

    private static int GetPageCapacity(ScriptPageSettings settings)
    {
        var (_, height) = GetPaperDimensions(settings.PaperSize);
        var contentHeight = Math.Max(72, height - (settings.TopMarginInches + settings.BottomMarginInches) * 72);
        return Math.Max(12, (int)Math.Floor(contentHeight / PointsPerLine));
    }

    private static int GetLineCost(string text, ScriptElementRule? rule, ScriptPageSettings settings, ScriptLayoutKind layout, bool columnWidth = false)
    {
        var wrappedLines = WrapLine(text, rule, settings, layout, columnWidth).Count;
        var spacing = (rule?.SpaceBeforePoints ?? 0) + (rule?.SpaceAfterPoints ?? 0);
        return wrappedLines + (int)Math.Ceiling(spacing / PointsPerLine);
    }

    private static (double Width, double Height) GetPaperDimensions(string paperSize) => paperSize switch
    {
        "A4" => (8.27 * 72, 11.69 * 72),
        "USLegal" => (8.5 * 72, 14 * 72),
        _ => (8.5 * 72, 11 * 72)
    };

    private static void AddScriptPage(List<ScriptPageLayout> pages, ScriptDocument document, int start, int length, ref int pageNumber)
    {
        var content = document.Text.Substring(start, length);
        pages.Add(new ScriptPageLayout(
            pages.Count,
            start,
            length,
            document.PageSettings.ShowPageNumbers ? document.PageSettings.FirstPageNumber + pageNumber : 0,
            false,
            content));
        pageNumber++;
    }
}
