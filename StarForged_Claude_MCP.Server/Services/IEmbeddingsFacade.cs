using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Server.Services;

public interface IEmbeddingsFacade
{
    Task<SearchResult[]> SearchAsync(string query, int topK = 3);

    Task<int[]> AddMemoryAsync(string text, string sourceDocument);

    Task<TextResult[]> RetrieveByIdsAsync(int[] ids);
}
