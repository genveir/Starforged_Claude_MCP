using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Server.Services;

public interface IDocumentsFacade
{
    Task<bool> AddDocumentAsync(string category, string filename, string content, string? summary, bool indexed);

    /// <summary>
    /// On all four write methods, a null summary leaves the stored one alone and an empty one clears it.
    /// A document that is currently indexed is re-indexed from the content the write leaves behind.
    /// </summary>
    Task<bool> UpdateDocumentAsync(string category, string filename, string content, string? summary);

    Task<bool> ReplaceSectionAsync(string category, string filename, string section, string text, string? summary);

    /// <summary>
    /// Replaces every occurrence of oldText within one section. Null when the document does not exist;
    /// otherwise the number of occurrences replaced.
    /// </summary>
    Task<int?> ReplaceSectionTextAsync(string category, string filename, string section, string oldText, string newText, string? summary);

    Task<bool> AppendAsync(string category, string filename, string? section, string text, string? summary);

    Task<bool> DeleteSectionAsync(string category, string filename, string section, string? summary);

    Task<bool> IndexDocumentAsync(string category, string filename);

    Task<bool> DeindexDocumentAsync(string category, string filename);

    Task<bool> DeleteDocumentAsync(string category, string filename);

    Task<Document?> GetDocumentAsync(string category, string filename);

    Task<DocumentIndexEntry?> GetDocumentSummaryAsync(string category, string filename);

    Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string category);

    /// <summary>
    /// Finds a literal word or phrase in the documents of a category and every category under it, or in
    /// just one document when a filename is given. Null when that one document does not exist.
    /// </summary>
    Task<TextSearchResult?> FindTextAsync(string category, string text, bool wholeWord, string? filename);

    Task<List<Beat>> GetCanonicalBeatsAsync(string category, int sessionNumber);

    /// <summary>
    /// The categories holding documents anywhere under this one. Empty exactly when it is a leaf.
    /// </summary>
    Task<List<string>> GetSubcategoriesAsync(string category);

    /// <summary>
    /// The categories above this one that hold documents themselves, which would stop it from being created.
    /// </summary>
    Task<List<string>> GetAncestorsHoldingDocumentsAsync(string category);
}
