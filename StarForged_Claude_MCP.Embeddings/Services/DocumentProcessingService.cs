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
        Task<int[]> IndexDocumentAsync(string documentText, int documentId, DocumentProcessorToUse processorToUse);

        Task RemoveIndexForDocumentAsync(int documentId);
    }

    internal class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly MarkdownPreprocessor markdownPreprocessor;
        private readonly UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor;
        private readonly EmbeddingsService embeddingsService;
        private readonly DbInterface dbInterface;

        public DocumentProcessingService(
            MarkdownPreprocessor markdownPreprocessor,
            UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor,
            EmbeddingsService embeddingsService,
            DbInterface dbInterface)
        {
            this.markdownPreprocessor = markdownPreprocessor;
            this.unchunkableFlatTextPreprocessor = unchunkableFlatTextPreprocessor;
            this.embeddingsService = embeddingsService;
            this.dbInterface = dbInterface;
        }

        public async Task<int[]> IndexDocumentAsync(string documentText, int documentId, DocumentProcessorToUse processorToUse)
        {
            var preprocessedText = processorToUse switch
            {
                DocumentProcessorToUse.Markdown => markdownPreprocessor.Process(documentText),
                DocumentProcessorToUse.Unchunkable => unchunkableFlatTextPreprocessor.Process(documentText),
                _ => throw new ArgumentException("No processor available for this type of document, it cannot be stored.")
            };

            await dbInterface.DeleteEmbeddingsForDocument(documentId);

            List<int> storedChunkIds = [];

            foreach (var chunk in preprocessedText.Chunks)
            {
                var embedding = embeddingsService.GenerateEmbeddings(chunk);
                storedChunkIds.Add(await dbInterface.WriteEmbedding(chunk, embedding, documentId));
            }

            return [.. storedChunkIds];
        }

        public async Task RemoveIndexForDocumentAsync(int documentId) =>
            await dbInterface.DeleteEmbeddingsForDocument(documentId);
    }
}
