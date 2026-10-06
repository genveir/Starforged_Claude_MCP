using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Server.Tools;

/// <summary>
/// Checks that several tools make before they act, each refusing with an <see cref="ArgumentException"/>
/// that tells the caller what to do instead.
/// </summary>
public class ToolGuards
{
    private readonly IDocumentsFacade _documents;
    private readonly IWritePermissions _writePermissions;
    private readonly ILogger<ToolGuards> _logger;

    public ToolGuards(IDocumentsFacade documents, IWritePermissions writePermissions, ILogger<ToolGuards> logger)
    {
        _documents = documents;
        _writePermissions = writePermissions;
        _logger = logger;
    }

    public void RequireWriteEnabled(Category category)
    {
        if (_writePermissions.IsWriteEnabled(category)) return;

        _logger.LogWarning("Refused a write to read-only category {Category}", category);
        throw new ArgumentException(
            $"Category '{category}' is read-only. Call request_write_permission for it before writing to any document in it.");
    }

    /// <summary>
    /// Only a leaf holds documents, so a tool that names one document, lists them or writes them needs one.
    /// Refusing a parent outright says so, where letting it through would only report the document missing.
    /// </summary>
    public async Task RequireLeafCategoryAsync(Category category)
    {
        var subcategories = await _documents.GetSubcategoriesAsync(category);
        if (subcategories.Count == 0) return;

        throw new ArgumentException(
            $"Category '{category}' is a parent category, not a leaf: this tool needs the full path of a category with " +
            $"no subcategories under it. The leaf categories under '{category}' are: {string.Join(", ", subcategories)}.");
    }

    public async Task RequireNoAncestorHoldsDocumentsAsync(Category category)
    {
        var ancestors = await _documents.GetAncestorsHoldingDocumentsAsync(category);
        if (ancestors.Count == 0) return;

        throw new ArgumentException(
            $"Category '{category}' cannot hold documents, because '{ancestors[0]}' above it already does: a category " +
            "holds either documents or subcategories, never both.");
    }

    public static ArgumentException NoSuchDocument(Category category, string filename) =>
        new($"No document named '{filename}' exists in category '{category}'.");

    public static string RequireDocumentWasFound(bool found, Category category, string filename, string message)
    {
        if (!found)
            throw NoSuchDocument(category, filename);

        return McpJson.Serialize(new { message });
    }
}
