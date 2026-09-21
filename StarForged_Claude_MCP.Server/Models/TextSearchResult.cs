namespace StarForged_Claude_MCP.Server.Models;

public record TextSearchResult(
    int TotalMatches,
    bool Truncated,
    List<TextSearchDocument> Documents);

public record TextSearchDocument(
    string Category,
    string Filename,
    string? Summary,
    int MatchCount,
    List<TextSearchSnippet> Snippets);

public record TextSearchSnippet(
    string? Section,
    string Text);
