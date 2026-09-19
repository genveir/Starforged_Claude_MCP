using StarForged_Claude_MCP.Embeddings.Database.Models;

namespace StarForged_Claude_MCP.Server.Services;

public interface IDocumentsFacade
{
    Task<bool> AddDocumentAsync(string category, string filename, string content, string? summary, bool indexed);

    Task<bool> UpdateDocumentAsync(string category, string filename, string content, string? summary, bool indexed);

    Task<bool> DeleteDocumentAsync(string category, string filename);

    Task<Document?> GetDocumentAsync(string category, string filename);

    Task<DocumentIndexEntry?> GetDocumentSummaryAsync(string category, string filename);

    Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string category);

    Task<List<Beat>> GetCanonicalBeatsAsync(string category, int sessionNumber);
}
