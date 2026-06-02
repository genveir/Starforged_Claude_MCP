using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Database.Models;

namespace StarForged_Claude_MCP.Server.Services;

public class DocumentsFacade
{
    private readonly DbInterface _dbInterface;

    public DocumentsFacade(DbInterface dbInterface)
    {
        _dbInterface = dbInterface;
    }

    public async Task StoreDocumentAsync(string content, string sourceDocument, string? summary = null, string? category = null) =>
        await _dbInterface.StoreDocument(content, sourceDocument, summary: summary, category: category);

    public async Task<List<DocumentResult>> GetDocumentsAsync(string sourceDocument, string? category = null) =>
        await _dbInterface.GetAllDocumentsForSourceDocument(sourceDocument, category);

    public async Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string? category = null) =>
        await _dbInterface.GetDistinctSourceDocuments(category);
}
