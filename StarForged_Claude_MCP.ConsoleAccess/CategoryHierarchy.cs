using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.ConsoleAccess;

/// <summary>
/// A category holds either documents or subcategories, never both. These checks report a breach of that on
/// stderr and return false, so a command can stop before it does anything.
/// </summary>
internal static class CategoryHierarchy
{
    public static async Task<bool> RequireLeaf(DbInterface dbInterface, string category)
    {
        var subcategories = await dbInterface.GetCategoriesUnder(category);
        if (subcategories.Count == 0) return true;

        Console.Error.WriteLine(
            $"Error: '{category}' is a parent category; use one of the leaf categories under it: {string.Join(", ", subcategories)}.");
        return false;
    }

    public static async Task<bool> RequireCanHoldDocuments(DbInterface dbInterface, string category)
    {
        if (!await RequireLeaf(dbInterface, category)) return false;

        var ancestors = await dbInterface.GetAncestorsHoldingDocuments(category);
        if (ancestors.Count == 0) return true;

        Console.Error.WriteLine(
            $"Error: '{category}' cannot hold documents, because '{ancestors[0]}' above it already does.");
        return false;
    }
}
