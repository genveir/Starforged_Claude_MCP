using System.Text.RegularExpressions;

namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public class BeatPreprocessor
{
    private static readonly Regex BeatPattern = new(@"\bBeat\s+(\d+)\.(\d+)\b", RegexOptions.Compiled);

    /// <summary>
    /// Finds the beat this text *is*, if it announces one. Text with no marker, and text whose
    /// only marker sits on the last line — the GM naming the beat that comes next rather than
    /// this one — is stored unnumbered, holding its own place in write order.
    /// </summary>
    public (int? beatNumber, int? version, string content) Process(string content)
    {
        var matches = BeatPattern.Matches(content);
        if (matches.Count == 0) return (null, null, content);

        var first = matches[0];

        if (matches.Count == 1)
        {
            var lines = content.Split('\n');
            var lastNonEmptyIndex = Array.FindLastIndex(lines, l => !string.IsNullOrWhiteSpace(l));
            if (BeatPattern.IsMatch(lines[lastNonEmptyIndex])) return (null, null, content);
        }

        return (int.Parse(first.Groups[1].Value), int.Parse(first.Groups[2].Value), content);
    }
}
