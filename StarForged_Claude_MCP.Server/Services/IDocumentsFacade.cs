using StarForged_Claude_MCP.Embeddings.Database.Models;

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

    Task<bool> AppendAsync(string category, string filename, string? section, string text, string? summary);

    Task<bool> DeleteSectionAsync(string category, string filename, string section, string? summary);

    Task<bool> IndexDocumentAsync(string category, string filename);

    Task<bool> DeindexDocumentAsync(string category, string filename);

    Task<bool> DeleteDocumentAsync(string category, string filename);

    Task<Document?> GetDocumentAsync(string category, string filename);

    Task<DocumentIndexEntry?> GetDocumentSummaryAsync(string category, string filename);

    Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string category);

    Task<List<Beat>> GetCanonicalBeatsAsync(string category, int sessionNumber);
}
