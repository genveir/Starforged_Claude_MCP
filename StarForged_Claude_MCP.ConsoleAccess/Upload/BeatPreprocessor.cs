using System.Text.RegularExpressions;

namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public class BeatPreprocessor
{
    private static readonly Regex BeatPattern = new(@"\bBeat\s+(\d+\.\d+)\b", RegexOptions.Compiled);

    public (string? beatNumber, string content) Process(string content)
    {
        var matches = BeatPattern.Matches(content);
        if (matches.Count == 0) return (null, content);
        if (matches.Count > 1) return (matches[0].Groups[1].Value, content);

        var lines = content.Split('\n');
        var lastNonEmptyIndex = Array.FindLastIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (BeatPattern.IsMatch(lines[lastNonEmptyIndex])) return (null, content);

        return (matches[0].Groups[1].Value, content);
    }
}
