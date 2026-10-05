using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Embeddings.Services.Preprocessing;

namespace StarForged_Claude_MCP.Embeddings.Services;

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
    private readonly EmbeddingsRepository embeddings;

    public DocumentProcessingService(
        MarkdownPreprocessor markdownPreprocessor,
        UnchunkableFlatTextPreprocessor unchunkableFlatTextPreprocessor,
        EmbeddingsService embeddingsService,
        EmbeddingsRepository embeddings)
    {
        this.markdownPreprocessor = markdownPreprocessor;
        this.unchunkableFlatTextPreprocessor = unchunkableFlatTextPreprocessor;
        this.embeddingsService = embeddingsService;
        this.embeddings = embeddings;
    }

    public async Task<int[]> IndexDocumentAsync(string documentText, int documentId, DocumentProcessorToUse processorToUse)
    {
        var preprocessedText = processorToUse switch
        {
            DocumentProcessorToUse.Markdown => markdownPreprocessor.Process(documentText),
            DocumentProcessorToUse.Unchunkable => unchunkableFlatTextPreprocessor.Process(documentText),
            _ => throw new ArgumentException("No processor available for this type of document, it cannot be stored.")
        };

        await embeddings.DeleteEmbeddingsForDocument(documentId);

        List<int> storedChunkIds = [];

        foreach (var chunk in preprocessedText.Chunks)
        {
            var embedding = embeddingsService.GenerateEmbeddings(chunk);
            storedChunkIds.Add(await embeddings.WriteEmbedding(
                text: chunk.Text,
                tokenCount: chunk.Tokens.Length,
                vector: embedding,
                documentId: documentId));
        }

        return [.. storedChunkIds];
    }

    public async Task RemoveIndexForDocumentAsync(int documentId) =>
        await embeddings.DeleteEmbeddingsForDocument(documentId);
}
