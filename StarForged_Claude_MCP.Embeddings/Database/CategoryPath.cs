namespace StarForged_Claude_MCP.Embeddings.Database;

/// <summary>
/// Categories form a tree written as dotted paths: 'Campaign.Oracles' is a subcategory of 'Campaign'. There is
/// no table of categories; the tree is whatever the stored paths spell out. A category either holds documents
/// (a leaf) or has subcategories under it (a parent), never both.
/// </summary>
public static class CategoryPath
{
    public const char Separator = '.';

    public static bool IsWellFormed(string category) =>
        category.Split(Separator).All(segment => !string.IsNullOrWhiteSpace(segment) && segment.Trim() == segment);

    /// <summary>
    /// Every category above this one, nearest the root first: 'A.B.C' gives 'A' and 'A.B'.
    /// </summary>
    public static List<string> Ancestors(string category)
    {
        var ancestors = new List<string>();

        for (var index = category.IndexOf(Separator); index >= 0; index = category.IndexOf(Separator, index + 1))
            ancestors.Add(category[..index]);

        return ancestors;
    }

    /// <summary>
    /// The start every path strictly under this category shares.
    /// </summary>
    internal static string DescendantPrefix(string category) => category + Separator;
}
