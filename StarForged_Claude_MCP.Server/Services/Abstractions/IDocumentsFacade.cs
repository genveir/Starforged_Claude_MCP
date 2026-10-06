using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Server.Services.Abstractions;

public interface IDocumentsFacade
{
    Task<bool> AddDocumentAsync(Category category, string filename, string content, string? summary, bool indexed);

    /// <summary>
    /// On all four write methods, a null summary leaves the stored one alone and an empty one clears it.
    /// A document that is currently indexed is re-indexed from the content the write leaves behind.
    /// Only <see cref="UpdateDocumentAsync"/> can change that: a non-null indexed turns indexing on or off.
    /// </summary>
    Task<bool> UpdateDocumentAsync(Category category, string filename, string content, string? summary, bool? indexed);

    Task<bool> ReplaceSectionAsync(Category category, string filename, string section, string text, string? summary);

    /// <summary>
    /// Replaces every occurrence of oldText within one section. Null when the document does not exist;
    /// otherwise the number of occurrences replaced.
    /// </summary>
    Task<int?> ReplaceSectionTextAsync(Category category, string filename, string section, string oldText, string newText, string? summary);

    Task<bool> AppendAsync(Category category, string filename, string? section, string text, string? summary);

    Task<bool> DeleteSectionAsync(Category category, string filename, string section, string? summary);

    Task<bool> DeleteDocumentAsync(Category category, string filename);

    Task<Document?> GetDocumentAsync(Category category, string filename);

    Task<DocumentIndexEntry?> GetDocumentSummaryAsync(Category category, string filename);

    Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(Category category);

    /// <summary>
    /// Finds a literal word or phrase in the documents of a category and every category under it, or in
    /// just one document when a filename is given. Null when that one document does not exist.
    /// </summary>
    Task<TextSearchResult?> FindTextAsync(Category category, string text, bool wholeWord, string? filename);

    /// <summary>
    /// The categories holding documents anywhere under this one. Empty exactly when it is a leaf.
    /// </summary>
    Task<List<string>> GetSubcategoriesAsync(Category category);

    /// <summary>
    /// The categories above this one that hold documents themselves, which would stop it from being created.
    /// </summary>
    Task<List<string>> GetAncestorsHoldingDocumentsAsync(Category category);
}
