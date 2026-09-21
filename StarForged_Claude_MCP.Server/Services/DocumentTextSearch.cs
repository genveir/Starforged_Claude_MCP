using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Server.Models;
using System.Text.RegularExpressions;

namespace StarForged_Claude_MCP.Server.Services;

/// <summary>
/// Finds a literal word or phrase in documents, ignoring case. Whitespace in the phrase matches any run of
/// whitespace, line breaks included, so a phrase is still found where a document wraps it onto the next line.
/// </summary>
public static class DocumentTextSearch
{
    public const int MaxDocuments = 25;
    public const int MaxSnippetsPerDocument = 5;
    private const int SnippetContext = 150;
    private const string Ellipsis = "…";

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// A stretch of content shown as one snippet. Lower and Upper bound it to the lines its matches are on,
    /// so a snippet never runs on into the paragraph or header next to it.
    /// </summary>
    private sealed record Window(int Start, int End, int FirstMatch, int LastMatchEnd, int Lower, int Upper);

    public static TextSearchResult Search(IEnumerable<Document> documents, string text, bool wholeWord)
    {
        var pattern = BuildPattern(text, wholeWord);

        var matched = documents
            .Select(document => SearchDocument(document, pattern))
            .Where(result => result.MatchCount > 0)
            .OrderByDescending(result => result.MatchCount)
            .ThenBy(result => result.Filename, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TextSearchResult(
            TotalMatches: matched.Sum(result => result.MatchCount),
            Truncated: matched.Count > MaxDocuments,
            Documents: matched.Take(MaxDocuments).ToList());
    }

    private static Regex BuildPattern(string text, bool wholeWord)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape);
        var phrase = string.Join(@"\s+", words);

        // Lookarounds rather than \b: a phrase that starts or ends in punctuation, like "[GM]", has no
        // word boundary at its edge for \b to anchor on.
        if (wholeWord) phrase = $@"(?<!\w){phrase}(?!\w)";

        return new Regex(phrase, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static TextSearchDocument SearchDocument(Document document, Regex pattern)
    {
        var content = document.Content.Replace("\r\n", "\n");
        var matches = pattern.Matches(content);

        if (matches.Count == 0)
            return new TextSearchDocument(document.Category, document.Filename, document.Summary, MatchCount: 0, Snippets: []);

        var lineStarts = LineStarts(content);
        var sections = MarkdownSectionEditor.SectionPathsByLine(content);

        var snippets = Windows(content, matches, lineStarts)
            .Take(MaxSnippetsPerDocument)
            .Select(window => new TextSearchSnippet(
                Section: sections[LineOf(window.FirstMatch, lineStarts)],
                Text: Render(content, window)))
            .ToList();

        return new TextSearchDocument(document.Category, document.Filename, document.Summary, matches.Count, snippets);
    }

    /// <summary>
    /// One window per match, except that a match falling inside the previous window's context widens that
    /// window instead of repeating most of its text in a snippet of its own.
    /// </summary>
    private static List<Window> Windows(string content, MatchCollection matches, List<int> lineStarts)
    {
        List<Window> windows = [];

        foreach (Match match in matches)
        {
            var matchEnd = match.Index + match.Length;
            var lower = lineStarts[LineOf(match.Index, lineStarts)];
            var upper = LineEnd(LineOf(Math.Max(match.Index, matchEnd - 1), lineStarts), lineStarts, content);

            var start = Math.Max(lower, match.Index - SnippetContext);
            var end = Math.Min(upper, matchEnd + SnippetContext);

            if (windows.Count > 0 && start < windows[^1].End)
            {
                var previous = windows[^1];
                windows[^1] = previous with
                {
                    End = Math.Max(previous.End, end),
                    LastMatchEnd = matchEnd,
                    Upper = Math.Max(previous.Upper, upper)
                };
                continue;
            }

            windows.Add(new Window(start, end, FirstMatch: match.Index, LastMatchEnd: matchEnd, lower, upper));
        }

        return windows;
    }

    /// <summary>
    /// Where the context was cut short of its line, the cut is moved inward to the nearest word boundary,
    /// never past a match, and marked with an ellipsis.
    /// </summary>
    private static string Render(string content, Window window)
    {
        var start = window.Start;
        var end = window.End;

        while (start > window.Lower && start < window.FirstMatch && !char.IsWhiteSpace(content[start - 1]))
            start++;

        while (end < window.Upper && end > window.LastMatchEnd && !char.IsWhiteSpace(content[end]))
            end--;

        var text = Whitespace.Replace(content[start..end].Trim(), " ");
        var prefix = start > window.Lower ? Ellipsis : string.Empty;
        var suffix = end < window.Upper ? Ellipsis : string.Empty;

        return prefix + text + suffix;
    }

    private static List<int> LineStarts(string content)
    {
        List<int> starts = [0];

        for (int i = 0; i < content.Length; i++)
            if (content[i] == '\n') starts.Add(i + 1);

        return starts;
    }

    private static int LineOf(int position, List<int> lineStarts)
    {
        var index = lineStarts.BinarySearch(position);
        return index >= 0 ? index : ~index - 1;
    }

    private static int LineEnd(int line, List<int> lineStarts, string content) =>
        line + 1 < lineStarts.Count ? lineStarts[line + 1] - 1 : content.Length;
}
