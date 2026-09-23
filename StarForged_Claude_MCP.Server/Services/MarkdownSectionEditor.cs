namespace StarForged_Claude_MCP.Server.Services;

/// <summary>
/// Section-level edits on a Markdown document. A section is a header line plus everything below it
/// up to the next header at the same or a higher level, so it always carries its nested subsections
/// with it. Sections are addressed by header text, matched ignoring case, optionally qualified with
/// ancestor headers: "Ironlander Customs &gt; Burial Rites".
/// </summary>
public static class MarkdownSectionEditor
{
    private const string PathSeparator = ">";

    public static string ReplaceSection(string content, string section, string replacement)
    {
        var document = MarkdownDocument.Parse(content);
        var target = document.Find(section);

        document.RequireOwnHeaderFirst(replacement, target);
        document.RequireNoHeaderEscapes(replacement, target, skipOwnHeader: true);

        var lines = document.Lines;
        var kept = Prefix(lines, upTo: target.Start);
        AppendBlock(kept, SplitLines(replacement));
        AppendTail(kept, lines, from: target.End);

        return document.Join(kept);
    }

    /// <summary>
    /// Replaces every occurrence of a literal snippet within one section (header line included) and
    /// reports how many were replaced. Unlike ReplaceSection, this leaves the rest of the section's
    /// text untouched, so only the changed snippet has to be written out.
    /// </summary>
    public static (string Content, int Replacements) ReplaceTextInSection(string content, string section, string oldText, string newText)
    {
        if (string.IsNullOrEmpty(oldText))
            throw new ArgumentException("oldText cannot be empty");

        var document = MarkdownDocument.Parse(content);
        var target = document.Find(section);

        var lines = document.Lines;
        var sectionText = string.Join('\n', lines[target.Start..target.End]);

        var normalizedOldText = oldText.Replace("\r\n", "\n");
        var normalizedNewText = newText.Replace("\r\n", "\n");

        var replacements = CountOccurrences(sectionText, normalizedOldText);
        if (replacements == 0)
            throw new ArgumentException($"'{oldText}' was not found in section '{target.Path}'.");

        var replacedText = sectionText.Replace(normalizedOldText, normalizedNewText);
        document.RequireNoHeaderEscapes(replacedText, target, skipOwnHeader: true);

        var kept = Prefix(lines, upTo: target.Start);
        AppendBlock(kept, SplitLines(replacedText));
        AppendTail(kept, lines, from: target.End);

        return (document.Join(kept), replacements);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    public static string AppendToSection(string content, string? section, string addition)
    {
        var document = MarkdownDocument.Parse(content);
        var target = section == null ? null : document.Find(section);

        if (target != null)
            document.RequireNoHeaderEscapes(addition, target, skipOwnHeader: false);

        var lines = document.Lines;
        var insertAt = target?.End ?? lines.Length;

        var kept = Prefix(lines, upTo: insertAt);
        AppendBlock(kept, SplitLines(addition));
        AppendTail(kept, lines, from: insertAt);

        return document.Join(kept);
    }

    /// <summary>
    /// For each line of the content, split on line breaks, the path of the innermost section it sits in,
    /// written the way the section tools accept it. Lines above the first header belong to no section.
    /// </summary>
    public static string?[] SectionPathsByLine(string content) =>
        MarkdownDocument.Parse(content).SectionPathsByLine();

    public static string DeleteSection(string content, string section)
    {
        var document = MarkdownDocument.Parse(content);
        var target = document.Find(section);

        var lines = document.Lines;
        var kept = Prefix(lines, upTo: target.Start);
        AppendTail(kept, lines, from: target.End);

        return document.Join(kept);
    }

    /// <summary>
    /// Everything before the edit point, with trailing blank lines dropped: the seam gets exactly one
    /// blank line back if anything is written after it.
    /// </summary>
    private static List<string> Prefix(string[] lines, int upTo)
    {
        List<string> kept = [.. lines.Take(upTo)];
        TrimTrailingBlanks(kept);
        return kept;
    }

    /// <summary>
    /// Written text arrives padded as often as not — a leading newline before a header, a trailing one
    /// after the last paragraph. The seam supplies its own single blank line, so the block is stripped
    /// at both ends rather than left to stack blank lines on top of it.
    /// </summary>
    private static void AppendBlock(List<string> kept, List<string> block)
    {
        TrimSurroundingBlanks(block);
        if (block.Count == 0) return;

        if (kept.Count > 0) kept.Add(string.Empty);
        kept.AddRange(block);
    }

    private static void AppendTail(List<string> kept, string[] lines, int from)
    {
        var tail = lines.Skip(from).ToList();
        if (tail.All(string.IsNullOrWhiteSpace)) return;

        if (kept.Count > 0) kept.Add(string.Empty);
        kept.AddRange(tail);
    }

    private static void TrimTrailingBlanks(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);
    }

    private static void TrimSurroundingBlanks(List<string> lines)
    {
        TrimTrailingBlanks(lines);

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);
    }

    private static List<string> SplitLines(string text) => [.. text.Replace("\r\n", "\n").Split('\n')];

    private sealed record Header(int Level, string Title, int LineIndex);

    private sealed record Section(Header Header, int Start, int End, string Path);

    private sealed class MarkdownDocument
    {
        private readonly List<Header> _headers;
        private readonly string _newline;

        private MarkdownDocument(string[] lines, List<Header> headers, string newline)
        {
            Lines = lines;
            _headers = headers;
            _newline = newline;
        }

        public string[] Lines { get; }

        public static MarkdownDocument Parse(string content)
        {
            // Documents are written back with whichever line ending they already predominantly use.
            var newline = content.Contains("\r\n") ? "\r\n" : "\n";
            var lines = content.Replace("\r\n", "\n").Split('\n');

            return new MarkdownDocument(lines, ParseHeaders(lines), newline);
        }

        public string Join(List<string> lines) => string.Join(_newline, lines);

        public Section Find(string section)
        {
            var path = SplitPath(section);

            var matches = _headers
                .Select((header, index) => index)
                .Where(index => MatchesPath(index, path))
                .ToList();

            if (matches.Count == 0)
                throw new ArgumentException(
                    $"No section matching '{section}' was found. {DescribeSections()}");

            if (matches.Count > 1)
                throw new ArgumentException(
                    $"'{section}' matches more than one section: {string.Join("; ", matches.Select(PathOf))}. " +
                    "Give more of the path to pick just one.");

            var matchedIndex = matches[0];
            return new Section(
                Header: _headers[matchedIndex],
                Start: _headers[matchedIndex].LineIndex,
                End: EndOfSection(matchedIndex),
                Path: PathOf(matchedIndex));
        }

        public string?[] SectionPathsByLine()
        {
            var paths = new string?[Lines.Length];

            for (int i = 0; i < _headers.Count; i++)
            {
                var path = PathOf(i);
                var end = i + 1 < _headers.Count ? _headers[i + 1].LineIndex : Lines.Length;

                for (int line = _headers[i].LineIndex; line < end; line++)
                    paths[line] = path;
            }

            return paths;
        }

        /// <summary>
        /// A replacement stands in for the section's own header line, so it has to begin with one at
        /// the same level. Without this check, text that omits the header silently folds the section's
        /// content into its parent, which can only be undone by rewriting the whole document.
        /// </summary>
        public void RequireOwnHeaderFirst(string replacement, Section target)
        {
            var lines = SplitLines(replacement);
            var first = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
            var header = first == null ? null : ReadHeader(first);

            if (header?.Level == target.Header.Level) return;

            var marker = new string('#', target.Header.Level);
            throw new ArgumentException(
                $"The replacement for '{target.Path}' must start with that section's own header line, at its current level: " +
                $"'{marker} {target.Header.Title}'. Rewrite the title there to rename the section.");
        }

        /// <summary>
        /// Text written into a section may only introduce headers below it. A shallower header would
        /// end the section it was written into and restructure everything after it.
        /// </summary>
        public void RequireNoHeaderEscapes(string text, Section target, bool skipOwnHeader)
        {
            var lines = SplitLines(text);
            var inFence = false;

            for (int i = skipOwnHeader ? FirstContentLine(lines) + 1 : 0; i < lines.Count; i++)
            {
                if (IsFence(lines[i]))
                {
                    inFence = !inFence;
                    continue;
                }
                if (inFence) continue;

                var header = ReadHeader(lines[i]);
                if (header == null || header.Level > target.Header.Level) continue;

                var deeper = new string('#', target.Header.Level + 1);
                throw new ArgumentException(
                    $"'{lines[i].Trim()}' is a level {header.Level} header, so it would end the section '{target.Path}' " +
                    $"rather than sit inside it. Headers written here have to start with at least '{deeper}'.");
            }
        }

        private static int FirstContentLine(List<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
                if (!string.IsNullOrWhiteSpace(lines[i])) return i;

            return lines.Count;
        }

        private int EndOfSection(int headerIndex)
        {
            var level = _headers[headerIndex].Level;

            for (int i = headerIndex + 1; i < _headers.Count; i++)
                if (_headers[i].Level <= level) return _headers[i].LineIndex;

            return Lines.Length;
        }

        /// <summary>
        /// The last path element names the section itself; any earlier ones have to appear, in order,
        /// among its ancestors. Levels in between may be left out, so "Customs &gt; Burial Rites"
        /// still finds a section whose full path is "Customs &gt; Rites &gt; Burial Rites".
        /// </summary>
        private bool MatchesPath(int headerIndex, string[] path)
        {
            if (!SameName(_headers[headerIndex].Title, path[^1])) return false;

            var remaining = path.Length - 2;
            var level = _headers[headerIndex].Level;

            for (int i = headerIndex - 1; i >= 0 && remaining >= 0; i--)
            {
                if (_headers[i].Level >= level) continue;

                level = _headers[i].Level;
                if (SameName(_headers[i].Title, path[remaining])) remaining--;
            }

            return remaining < 0;
        }

        private string PathOf(int headerIndex)
        {
            List<string> ancestors = [_headers[headerIndex].Title];
            var level = _headers[headerIndex].Level;

            for (int i = headerIndex - 1; i >= 0 && level > 1; i--)
            {
                if (_headers[i].Level >= level) continue;

                level = _headers[i].Level;
                ancestors.Insert(0, _headers[i].Title);
            }

            return string.Join(" > ", ancestors);
        }

        private string DescribeSections() =>
            _headers.Count == 0
                ? "The document has no headers, so it has no sections to address; use update_document to rewrite it."
                : $"The document's sections are: {string.Join("; ", _headers.Select((_, index) => PathOf(index)))}.";

        private static string[] SplitPath(string section)
        {
            if (section.Contains("&gt;", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"'{section}' contains '&gt;', the HTML-escaped form of '>'. " +
                    "Send a literal '>' between path segments instead, e.g. 'Parent > Child'.");

            var path = section
                .Split(PathSeparator)
                .Select(part => part.Trim().Trim('#').Trim())
                .Where(part => part.Length > 0)
                .ToArray();

            if (path.Length == 0)
                throw new ArgumentException("Section cannot be empty");

            return path;
        }

        private static List<Header> ParseHeaders(string[] lines)
        {
            List<Header> headers = [];
            var inFence = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (IsFence(lines[i]))
                {
                    inFence = !inFence;
                    continue;
                }
                if (inFence) continue;

                var header = ReadHeader(lines[i]);
                if (header != null) headers.Add(header with { LineIndex = i });
            }

            return headers;
        }

        private static bool IsFence(string line)
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith("```") || trimmed.StartsWith("~~~");
        }

        private static Header? ReadHeader(string line)
        {
            var trimmed = line.Trim();

            var level = 0;
            while (level < trimmed.Length && trimmed[level] == '#') level++;

            if (level is 0 or > 6) return null;
            if (level >= trimmed.Length || trimmed[level] != ' ') return null;

            var title = trimmed[level..].Trim().TrimEnd('#').Trim();
            if (title.Length == 0) return null;

            return new Header(level, title, LineIndex: 0);
        }

        private static bool SameName(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
