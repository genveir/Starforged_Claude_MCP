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

    public async Task StoreDocumentAsync(string content, string sourceDocument) =>
        await _dbInterface.StoreDocument(content, sourceDocument);

    public async Task<List<DocumentResult>> GetDocumentsAsync(string sourceDocument) =>
        await _dbInterface.GetAllDocumentsForSourceDocument(sourceDocument);
}
