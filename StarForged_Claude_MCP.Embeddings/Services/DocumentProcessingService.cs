using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services.Preprocessing;

namespace StarForged_Claude_MCP.Embeddings.Services
{
    public enum DocumentProcessorToUse
    {
        None,
        Unchunkable,
        Markdown
    }

    public interface IDocumentProcessingService
    {
        Task<int[]> ProcessAndStoreDocumentAsync(string documentText, string sourceDocument, DocumentProcessorToUse processorToUse);
    }

    internal class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly MarkdownPreprocessor markdownPreprocessor;
        private readonly UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor;
        private readonly EmbeddingsService embeddingsService;
        private readonly DbInterface dbInterface;
        private readonly VectorCacheService vectorCache;

        public DocumentProcessingService(
            MarkdownPreprocessor markdownPreprocessor,
            UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor,
            EmbeddingsService embeddingsService,
            DbInterface dbInterface,
            VectorCacheService vectorCache)
        {
            this.markdownPreprocessor = markdownPreprocessor;
            this.unchunkableFlatTextPreprocessor = unchunkableFlatTextPreprocessor;
            this.embeddingsService = embeddingsService;
            this.dbInterface = dbInterface;
            this.vectorCache = vectorCache;
        }

        public async Task<int[]> ProcessAndStoreDocumentAsync(string documentText, string sourceDocument, DocumentProcessorToUse processorToUse)
        {
            var preprocessedText = processorToUse switch
            {
                DocumentProcessorToUse.Markdown => markdownPreprocessor.Process(documentText),
                DocumentProcessorToUse.Unchunkable => unchunkableFlatTextPreprocessor.Process(documentText),
                _ => throw new ArgumentException("No processor available for this type of document, it cannot be stored.")
            };

            List<int> storedChunkIds = [];

            foreach (var chunk in preprocessedText.Chunks)
            {
                var embedding = embeddingsService.GenerateEmbeddings(chunk);

                var vectors = await vectorCache.GetAllVectors();
                if (vectors.ContainsValue(embedding))
                {
                    continue;
                }

                var storedChunkId = await dbInterface.WriteEmbedding(chunk, embedding, sourceDocument);
                storedChunkIds.Add(storedChunkId);
            }

            return [.. storedChunkIds];
        }
    }
}
