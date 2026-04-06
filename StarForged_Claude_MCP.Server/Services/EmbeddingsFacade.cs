using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Server.Services;

public class EmbeddingsFacade
{
    private readonly ISearchService searchService;
    private readonly IDocumentProcessingService documentProcessingService;
    private readonly DbInterface dbInterface;

    public EmbeddingsFacade(ISearchService searchService,
        IDocumentProcessingService documentProcessingService,
        DbInterface dbInterface)
    {
        this.searchService = searchService;
        this.documentProcessingService = documentProcessingService;
        this.dbInterface = dbInterface;
    }

    public async Task<SearchResult[]> SearchAsync(string query, int topK = 3) => await searchService.Search(query, topK);

    public async Task<int[]> AddMemoryAsync(string text, string sourceDocument) =>
        await documentProcessingService.ProcessAndStoreDocumentAsync(text, sourceDocument, DocumentProcessorToUse.Markdown);
}
