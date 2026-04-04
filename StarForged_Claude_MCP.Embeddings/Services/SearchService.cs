using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using StarForged_Claude_MCP.Embeddings.Services.Preprocessing;

namespace StarForged_Claude_MCP.Embeddings.Services
{
    public interface ISearchService
    {
        Task<string[]> Search(string input, int topK);
    }

    internal class SearchService : ISearchService
    {
        private readonly VectorCacheService vectorCache;
        private readonly UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor;
        private readonly EmbeddingsService embeddingsService;
        private readonly DbInterface dbInterface;

        public SearchService(VectorCacheService vectorCache,
            UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor,
            EmbeddingsService embeddingsService,
            DbInterface dbInterface)
        {
            this.vectorCache = vectorCache;
            this.unchunkableFlatTextPreprocessor = unchunkableFlatTextPreprocessor;
            this.embeddingsService = embeddingsService;
            this.dbInterface = dbInterface;
        }

        public async Task<string[]> Search(string input, int topK)
        {
            var inputChunk = unchunkableFlatTextPreprocessor.Process(input).Chunks.Single();

            var ids = await PerformSimilaritySearch(inputChunk, topK);

            var textResults = await dbInterface.GetTextByIds(ids);

            var results = ids
                .Select(id => textResults.FirstOrDefault(e => e.Id == id)?.Text)
                .OfType<string>()
                .ToArray();

            return results;
        }

        private async Task<int[]> PerformSimilaritySearch(Chunk input, int topK)
        {
            var queryVector = embeddingsService.GenerateEmbeddings(input);

            var vectors = await vectorCache.GetAllVectors();

            var similarities = vectors
                .Select(kvp => new { Id = kvp.Key, Similarity = CosineSimilarity(queryVector, kvp.Value) })
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Select(x => x.Id)
                .ToArray();

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
