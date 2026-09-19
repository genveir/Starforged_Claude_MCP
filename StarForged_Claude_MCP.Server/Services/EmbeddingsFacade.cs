using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Server.Services;

public class EmbeddingsFacade : IEmbeddingsFacade
{
    private readonly ISearchService searchService;
    private readonly DbInterface dbInterface;

    public EmbeddingsFacade(ISearchService searchService, DbInterface dbInterface)
    {
        this.searchService = searchService;
        this.dbInterface = dbInterface;
    }

    public async Task<SearchResult[]> SearchAsync(string query, string category, int topK = 3) =>
        await searchService.Search(query, category, topK);

    public async Task<TextResult[]> RetrieveByIdsAsync(int[] ids)
    {
        var results = await dbInterface.GetEmbeddedTextByIds(ids);
        var byId = results.ToDictionary(r => r.Id);
        return ids
            .Where(id => byId.ContainsKey(id))
            .Select(id => byId[id])
            .ToArray();
    }
}
