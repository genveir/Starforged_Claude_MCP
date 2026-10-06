using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Server.Services.Abstractions;

public interface IEmbeddingsFacade
{
    Task<SearchResult[]> SearchAsync(string query, Category category, int topK = 3);

    Task<TextResult[]> RetrieveByIdsAsync(int[] ids);
}
