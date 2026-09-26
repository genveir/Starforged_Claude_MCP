namespace StarForged_Claude_MCP.Server.Tools;

/// <summary>
/// Argument descriptions that several tools share word for word, so that the model reads one account of
/// categories, sections and summaries whichever tool it is looking at.
/// </summary>
public static class ToolDescriptions
{
    public const string LeafCategoryRule =
        "its full dotted path, such as 'Campaign.Oracles'. A leaf is a category with no subcategories under it; " +
        "a parent such as 'Campaign' is refused, and the refusal lists the leaf categories under it.";

    public const string LeafCategory = "Leaf category the document belongs to: " + LeafCategoryRule;

    public const string ScopeCategory =
        "Category to search, at any level. Categories are dotted paths: 'Campaign.Oracles' is a subcategory of " +
        "'Campaign'. A parent category covers every category under it, and a leaf covers only itself.";

    public const string MarkdownStructure =
        "Write well-formed Markdown: open with a '#' header and divide the rest under '##' headers. Sections are what " +
        "the document is chunked on, their titles are what search results are labelled with, and they are what the " +
        "section tools address. Content placed before the first header is stored, but search results for it carry no " +
        "section label and no section tool can reach it.";

    public const string ReplacementSummary =
        "Optional. Replaces the document's summary, shown alongside the filename whenever the category's documents " +
        "are listed. Leave it out to keep the summary the document already has; pass an empty string to clear it.";

    /// <summary>The section argument of a tool that acts on exactly one section, e.g. "replace" or "delete".</summary>
    public static string Section(string action) =>
        $"The section to {action}, named by its header text without the '#' markers and matched ignoring case, e.g. " +
        "'Burial Rites'. Where one name is ambiguous, qualify it with headers it sits under, separated by '>', e.g. " +
        "'Ironlander Customs > Burial Rites'. If nothing matches, or more than one section does, the error lists the " +
        "document's sections.";
}
