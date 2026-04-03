using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services;

namespace StarForged_Claude_MCP.Server.Services;

public class EmbeddingsFacade
{
    private readonly EmbeddingsService _embeddingsService;
    private readonly IDbInterface _dbInterface;
    private readonly VectorCacheService _vectorCache;
    private readonly MarkdownChunker _markdownChunker;

    public EmbeddingsFacade(
        EmbeddingsService embeddingsService,
        IDbInterface dbInterface,
        VectorCacheService vectorCache,
        MarkdownChunker markdownChunker)
    {
        _embeddingsService = embeddingsService;
        _dbInterface = dbInterface;
        _vectorCache = vectorCache;
        _markdownChunker = markdownChunker;
    }

    public async Task<string[]> SearchAsync(string query, int topK = 3)
    {
        var ids = await _embeddingsService.PerformSimilaritySearch(query, topK);

        var textResults = await _dbInterface.GetTextByIds(ids);

        var results = ids
            .Select(id => textResults.FirstOrDefault(e => e.Id == id)?.Text)
            .OfType<string>()
            .ToArray();

        return results;
    }

    public async Task<int> AddMemoryAsync(string text, string sourceDocument)
    {
        var tokenCount = _embeddingsService.CountTokens(text);
        var vector = await _embeddingsService.GenerateEmbeddings(text);

        var existingId = await _vectorCache.FindExistingVector(vector);
        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var id = await _dbInterface.WriteEmbedding(text, vector, sourceDocument, tokenCount);
        await _vectorCache.RefreshCache();
        return id;
    }

    public async Task DeleteMemoryAsync(int id)
    {
        await _dbInterface.DeleteEmbedding(id);
        await _vectorCache.RefreshCache();
    }

    public async Task<object?> GetMemoryAsync(int id)
    {
        var result = await _dbInterface.GetText(id);
        return result;
    }

    public async Task<object> UploadFileAsync(string text, string sourceDocument)
    {
        var chunks = _markdownChunker.ChunkBySection(text);
        var ids = new List<int>();

        foreach (var chunk in chunks)
        {
            var tokenCount = _embeddingsService.CountTokens(chunk);
            var vector = await _embeddingsService.GenerateEmbeddings(chunk);

            var existingId = await _vectorCache.FindExistingVector(vector);
            if (existingId.HasValue)
            {
                ids.Add(existingId.Value);
            }
            else
            {
                var id = await _dbInterface.WriteEmbedding(chunk, vector, sourceDocument, tokenCount);
                ids.Add(id);
            }
        }

        await _vectorCache.RefreshCache();

        return new { ChunkCount = chunks.Count, Ids = ids };
    }
}
