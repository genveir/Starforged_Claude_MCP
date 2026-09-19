using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Database.Models;

namespace StarForged_Claude_MCP.Server.Services;

public class DocumentsFacade : IDocumentsFacade
{
    private readonly DbInterface _dbInterface;

    public DocumentsFacade(DbInterface dbInterface)
    {
        _dbInterface = dbInterface;
    }

    public async Task StoreDocumentAsync(string content, string sourceDocument, string category, string? summary = null) =>
        await _dbInterface.StoreDocument(content, sourceDocument, category, summary: summary);

    public async Task<List<DocumentResult>> GetDocumentsAsync(string sourceDocument, string category) =>
        await _dbInterface.GetAllDocumentsForSourceDocument(sourceDocument, category);

    public async Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string category) =>
        await _dbInterface.GetDistinctSourceDocuments(category);
}
