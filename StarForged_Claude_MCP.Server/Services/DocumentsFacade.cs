using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services;

namespace StarForged_Claude_MCP.Server.Services;

public class DocumentsFacade : IDocumentsFacade
{
    private readonly DbInterface _dbInterface;
    private readonly IDocumentProcessingService _documentProcessing;

    public DocumentsFacade(DbInterface dbInterface, IDocumentProcessingService documentProcessing)
    {
        _dbInterface = dbInterface;
        _documentProcessing = documentProcessing;
    }

    public async Task<bool> AddDocumentAsync(string category, string filename, string content, string? summary, bool indexed)
    {
        if (await _dbInterface.GetDocument(category, filename) != null) return false;

        var id = await _dbInterface.StoreDocument(category, filename, content, summary, indexed);

        if (indexed)
        {
            await _documentProcessing.IndexDocumentAsync(content, id, DocumentProcessorToUse.Markdown);
        }

        return true;
    }

    public async Task<bool> UpdateDocumentAsync(string category, string filename, string content, string? summary, bool indexed)
    {
        var existing = await _dbInterface.GetDocument(category, filename);
        if (existing == null) return false;

        await _dbInterface.UpdateDocument(existing.Id, content, summary, indexed);

        // An update replaces the content outright, so whatever was indexed for it is stale either way.
        if (indexed)
        {
            await _documentProcessing.IndexDocumentAsync(content, existing.Id, DocumentProcessorToUse.Markdown);
        }
        else
        {
            await _documentProcessing.RemoveIndexForDocumentAsync(existing.Id);
        }

        return true;
    }

    public async Task<bool> DeleteDocumentAsync(string category, string filename)
    {
        var existing = await _dbInterface.GetDocument(category, filename);
        if (existing == null) return false;

        // Chunks go with it: the foreign key cascades.
        await _dbInterface.DeleteDocument(existing.Id);
        return true;
    }

    public async Task<Document?> GetDocumentAsync(string category, string filename) =>
        await _dbInterface.GetDocument(category, filename);

    public async Task<DocumentIndexEntry?> GetDocumentSummaryAsync(string category, string filename) =>
        await _dbInterface.GetDocumentSummary(category, filename);

    public async Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string category) =>
        await _dbInterface.GetDocumentIndex(category);

    public async Task<List<Beat>> GetCanonicalBeatsAsync(string category, int sessionNumber) =>
        CanonicalBeats.Select(await _dbInterface.GetBeatsForSession(category, sessionNumber));
}
