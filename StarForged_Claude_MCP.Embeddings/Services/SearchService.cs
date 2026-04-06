using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using StarForged_Claude_MCP.Embeddings.Services.Preprocessing;

namespace StarForged_Claude_MCP.Embeddings.Services
{
    public interface ISearchService
    {
        Task<SearchResult[]> Search(string input, int topK);
    }

    internal class SearchService : ISearchService
    {
        private readonly VectorCacheService vectorCache;
        private readonly UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor;
        private readonly EmbeddingsService embeddingsService;
        private readonly DbInterface dbInterface;
        private readonly ILogger<SearchService> logger;

        public SearchService(VectorCacheService vectorCache,
            UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor,
            EmbeddingsService embeddingsService,
            DbInterface dbInterface,
            ILogger<SearchService> logger)
        {
            this.vectorCache = vectorCache;
            this.unchunkableFlatTextPreprocessor = unchunkableFlatTextPreprocessor;
            this.embeddingsService = embeddingsService;
            this.dbInterface = dbInterface;
            this.logger = logger;
        }

        public async Task<SearchResult[]> Search(string input, int topK)
        {
            logger.LogInformation("Starting search with input: {Input} and topK: {TopK}", input, topK);

            var inputChunk = unchunkableFlatTextPreprocessor.Process(input).Chunks.Single();

            var similarityResults = await PerformSimilaritySearch(inputChunk, topK);

            var ids = similarityResults.Select(r => r.Id).ToArray();

            var textResults = await dbInterface.GetEmbeddedTextByIds(ids);

            var results = similarityResults
                .Join(textResults, sim => sim.Id, text => text.Id, (sim, text) => new SearchResult(Text: text.Text, SimilarityScore: sim.SimilarityScore))
                .ToArray();

            return results;
        }

        private async Task<SimilarityResult[]> PerformSimilaritySearch(Chunk input, int topK)
        {
            var queryVector = embeddingsService.GenerateEmbeddings(input);

            logger.LogDebug("Query vector for input: {QueryVector}", queryVector);

            var vectors = await vectorCache.GetAllVectors();

            logger.LogDebug("Vector count on similarity search: {VectorCount}", vectors.Count);

            var similarities = vectors
                .Select(kvp => new { Id = kvp.Key, Similarity = CosineSimilarity(queryVector, kvp.Value) })
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Select(x => new SimilarityResult(Id: x.Id, SimilarityScore: x.Similarity))
                .ToArray();

            logger.LogInformation("Similarity search completed. Top {TopK} IDs: {Ids}", topK, similarities);

            return similarities;
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0;

            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            float magnitude = MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB);
            return magnitude == 0 ? 0 : dotProduct / magnitude;
        }
    }
}
