using StarForged_Claude_MCP.Embeddings.Database.Models;

namespace StarForged_Claude_MCP.Server.Services;

public interface IDocumentsFacade
{
    Task StoreDocumentAsync(string content, string sourceDocument, string? summary = null, string? category = null);

    Task<List<DocumentResult>> GetDocumentsAsync(string sourceDocument, string? category = null);

    Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string? category = null);
}
