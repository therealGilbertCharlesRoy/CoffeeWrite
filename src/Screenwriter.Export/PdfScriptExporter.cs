using System.Globalization;
using System.Text;
using Screenwriter.Core.Documents;
using Screenwriter.Core.Formatting;
using Screenwriter.Editor;

namespace Screenwriter.Export;

public static class PdfScriptExporter
{
    private const double FontSize = 12;
    private const double LineHeight = 12;

    public static async Task ExportAsync(ScriptDocument document, ScriptFormatProfile profile, string path, CancellationToken cancellationToken = default)
    {
        var pages = RenderPages(document, profile);
        var objects = new List<byte[]>();
        objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add([]);
        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>"));

        var pageObjectIds = new List<int>();
        foreach (var page in pages)
        {
            var pageId = objects.Count + 1;
            var contentId = pageId + 1;
            pageObjectIds.Add(pageId);
            objects.Add(Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(page.Width)} {F(page.Height)}] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>"));
            var stream = Ascii(page.Content);
            objects.Add(Concat(Ascii($"<< /Length {stream.Length} >>\nstream\n"), stream, Ascii("\nendstream")));
        }

        objects[1] = Ascii($"<< /Type /Pages /Count {pageObjectIds.Count} /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] >>");
        await WritePdfAsync(path, objects, cancellationToken);
    }

    private static IReadOnlyList<RenderedPage> RenderPages(ScriptDocument document, ScriptFormatProfile profile)
    {
        var layouts = ScriptPaginationService.Paginate(document, profile);
        var (width, height) = Paper(document.PageSettings.PaperSize);
        return layouts.Select(layout => layout.IsTitlePage
            ? RenderTitlePage(document, width, height)
            : RenderScriptPage(document, profile, layout, width, height)).ToArray();
    }

    private static RenderedPage RenderTitlePage(ScriptDocument document, double width, double height)
    {
        var page = new RenderedPage(width, height);
        var title = string.IsNullOrWhiteSpace(document.Title) ? "Untitled screenplay" : document.Title;
        var titleY = height / 2 + 24;
        page.Center(title, titleY, 16);
        var subtitleY = titleY - 26;
        if (!string.IsNullOrWhiteSpace(document.TitlePage.Subtitle))
        {
            page.Center(document.TitlePage.Subtitle, subtitleY, 12);
            subtitleY -= 24;
        }
        page.Center("by", subtitleY, 12);
        page.Center(document.TitlePage.Author, subtitleY - 22, 12);
        page.Text(document.TitlePage.DraftDate, document.PageSettings.LeftMarginInches * 72, document.PageSettings.BottomMarginInches * 72 + 32, 10);
        page.Text(document.TitlePage.Contact, document.PageSettings.LeftMarginInches * 72, document.PageSettings.BottomMarginInches * 72 + 18, 10);
        return page;
    }

    private static RenderedPage RenderScriptPage(ScriptDocument document, ScriptFormatProfile profile, ScriptPageLayout layout, double width, double height)
    {
        var page = new RenderedPage(width, height);
        if (document.PageSettings.ShowPageNumbers && layout.PageNumber > 0)
            page.Right(layout.PageNumber.ToString(CultureInfo.InvariantCulture), width - document.PageSettings.RightMarginInches * 72, height - document.PageSettings.TopMarginInches * 72 + 12, 10);

        var lines = layout.Content.Split('\n');
        var firstLine = layout.StartOffset == 0 ? 0 : document.Text[..layout.StartOffset].Count(character => character == '\n');
        var xBase = document.PageSettings.LeftMarginInches * 72;
        var y = height - document.PageSettings.TopMarginInches * 72 - LineHeight;
        var available = Math.Max(36, width - (document.PageSettings.LeftMarginInches + document.PageSettings.RightMarginInches) * 72);
        for (var i = 0; i < lines.Length; i++)
        {
            var typeIndex = firstLine + i;
            var type = typeIndex < document.Elements.Count ? document.Elements[typeIndex].Type : ScriptElementType.Action;
            var rule = profile.FindRule(type);
            var text = lines[i].TrimEnd('\r');
            if (rule?.Uppercase == true)
                text = text.ToUpperInvariant();

            if (profile.Layout == ScriptLayoutKind.TwoColumnAudioVisual && type is ScriptElementType.Audio or ScriptElementType.Visual)
            {
                var left = type == ScriptElementType.Audio ? text : string.Empty;
                var right = type == ScriptElementType.Visual ? text : string.Empty;
                var rightType = type;
                if (i + 1 < lines.Length && firstLine + i + 1 < document.Elements.Count)
                {
                    rightType = document.Elements[firstLine + i + 1].Type;
                    if ((rightType is ScriptElementType.Audio or ScriptElementType.Visual) && rightType != type)
                    {
                        var other = lines[i + 1].TrimEnd('\r');
                        if (profile.FindRule(rightType)?.Uppercase == true)
                            other = other.ToUpperInvariant();
                        if (rightType == ScriptElementType.Audio) left = other;
                        else right = other;
                        i++;
                    }
                }
                y -= Math.Max(rule?.SpaceBeforePoints ?? 0, profile.FindRule(rightType)?.SpaceBeforePoints ?? 0);
                var columnWidth = (available - 12) / 2;
                var leftLines = ScriptPaginationService.WrapLine(left, profile.FindRule(ScriptElementType.Audio), document.PageSettings, profile.Layout, columnWidth: true);
                var rightLines = ScriptPaginationService.WrapLine(right, profile.FindRule(ScriptElementType.Visual), document.PageSettings, profile.Layout, columnWidth: true);
                var rowCount = Math.Max(leftLines.Count, rightLines.Count);
                for (var row = 0; row < rowCount; row++)
                {
                    if (row < leftLines.Count) page.Text(leftLines[row], xBase, y, FontSize);
                    if (row < rightLines.Count) page.Text(rightLines[row], xBase + columnWidth + 12, y, FontSize);
                    y -= LineHeight;
                }
                y -= Math.Max(rule?.SpaceAfterPoints ?? 0, profile.FindRule(rightType)?.SpaceAfterPoints ?? 0);
                continue;
            }

            y -= rule?.SpaceBeforePoints ?? 0;
            var indentLeft = (rule?.LeftIndentInches ?? 0) * 72;
            foreach (var wrapped in ScriptPaginationService.WrapLine(text, rule, document.PageSettings, profile.Layout))
            {
                page.Text(wrapped, xBase + indentLeft, y, FontSize);
                y -= LineHeight;
            }
            y -= rule?.SpaceAfterPoints ?? 0;
        }
        return page;
    }

    private static async Task WritePdfAsync(string path, IReadOnlyList<byte[]> objects, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var offsets = new List<long> { 0 };
        await stream.WriteAsync(Ascii("%PDF-1.4\n% Draftroom\n"), cancellationToken);
        foreach (var obj in objects)
        {
            offsets.Add(stream.Position);
            await stream.WriteAsync(Ascii($"{offsets.Count - 1} 0 obj\n"), cancellationToken);
            await stream.WriteAsync(obj, cancellationToken);
            await stream.WriteAsync(Ascii("\nendobj\n"), cancellationToken);
        }
        var xrefOffset = stream.Position;
        await stream.WriteAsync(Ascii($"xref\n0 {offsets.Count}\n0000000000 65535 f \n"), cancellationToken);
        foreach (var offset in offsets.Skip(1))
            await stream.WriteAsync(Ascii($"{offset:0000000000} 00000 n \n"), cancellationToken);
        await stream.WriteAsync(Ascii($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"), cancellationToken);
    }

    private static (double Width, double Height) Paper(string paperSize) => paperSize switch
    {
        "A4" => (595.44, 841.68),
        "USLegal" => (612, 1008),
        _ => (612, 792)
    };

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    private sealed class RenderedPage(double width, double height)
    {
        private readonly StringBuilder _content = new();
        public double Width { get; } = width;
        public double Height { get; } = height;
        public string Content => _content.ToString();

        public void Text(string text, double x, double y, double size)
        {
            var encoded = PdfText(text);
            _content.Append("BT /F1 ").Append(F(size)).Append(" Tf ").Append(F(x)).Append(' ').Append(F(y)).Append(" Td <").Append(Convert.ToHexString(encoded)).Append("> Tj ET\n");
        }

        public void Center(string text, double y, double size)
        {
            var safe = PdfText(text);
            var x = (Width - safe.Length * size * 0.6) / 2;
            Text(text, x, y, size);
        }

        public void Right(string text, double x, double y, double size)
        {
            var safe = PdfText(text);
            Text(text, x - safe.Length * size * 0.6, y, size);
        }
    }

    private static byte[] PdfText(string value)
    {
        var result = new List<byte>(value.Length);
        foreach (var character in value)
        {
            var mapped = character switch
            {
                '—' or '–' => "--",
                '“' or '”' => "\"",
                '‘' or '’' => "'",
                '…' => "...",
                '•' => "*",
                _ when character is >= ' ' and <= '~' => character.ToString(),
                _ when character <= 255 => character.ToString(),
                _ => "?"
            };
            result.AddRange(Encoding.Latin1.GetBytes(mapped));
        }
        return result.ToArray();
    }
}
