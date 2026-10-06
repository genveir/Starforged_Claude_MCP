using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Embeddings.Services;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Server.Services;

public class EmbeddingsFacade : IEmbeddingsFacade
{
    private readonly ISearchService searchService;
    private readonly EmbeddingsRepository embeddings;

    public EmbeddingsFacade(ISearchService searchService, EmbeddingsRepository embeddings)
    {
        this.searchService = searchService;
        this.embeddings = embeddings;
    }

    public async Task<SearchResult[]> SearchAsync(string query, Category category, int topK = 3) =>
        await searchService.Search(query, category, topK);

    public async Task<TextResult[]> RetrieveByIdsAsync(int[] ids)
    {
        var results = await embeddings.GetEmbeddedTextByIds(ids);
        var byId = results.ToDictionary(r => r.Id);
        return ids
            .Where(id => byId.ContainsKey(id))
            .Select(id => byId[id])
            .ToArray();
    }
}
