using StarForged_Claude_MCP.Embeddings.Database.Models;

namespace StarForged_Claude_MCP.Server.Services;

public interface IDocumentsFacade
{
    /// <summary>Stores a new document. Returns false if the category already holds that filename.</summary>
    Task<bool> AddDocumentAsync(string category, string filename, string content, string? summary, bool indexed);

    /// <summary>Replaces a document's content wholesale. Returns false if it does not exist.</summary>
    Task<bool> UpdateDocumentAsync(string category, string filename, string content, string? summary, bool indexed);

    /// <summary>Returns false if the document does not exist.</summary>
    Task<bool> DeleteDocumentAsync(string category, string filename);

    Task<Document?> GetDocumentAsync(string category, string filename);

    /// <summary>The document's summary and flags, without its content. Null if it does not exist.</summary>
    Task<DocumentIndexEntry?> GetDocumentSummaryAsync(string category, string filename);

    Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string category);

    Task<List<Beat>> GetCanonicalBeatsAsync(string category, int sessionNumber);
}
