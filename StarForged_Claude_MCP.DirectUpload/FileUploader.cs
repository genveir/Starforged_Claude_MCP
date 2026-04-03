using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services;

namespace StarForged_Claude_MCP.DirectUpload;

public class FileUploader
{
    private readonly EmbeddingsService _embeddingsService;
    private readonly IDbInterface _dbInterface;
    private readonly VectorCacheService _vectorCache;
    private readonly MarkdownChunker _markdownChunker;

    public FileUploader(
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

    public async Task UploadFolderAsync(string folderPath)
    {
        var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

        Console.WriteLine($"Found {files.Length} file(s) to process.");

        var totalChunks = 0;

        foreach (var filePath in files)
        {
            try
            {
                Console.WriteLine($"Processing: {filePath}");

                var text = await File.ReadAllTextAsync(filePath);
                var result = await UploadFileAsync(text, filePath);

                Console.WriteLine($"  Uploaded {result.ChunkCount} chunk(s) with IDs: {string.Join(", ", result.Ids)}");
                totalChunks += result.ChunkCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error processing file: {ex.Message}");
            }
        }

        Console.WriteLine($"\nCompleted! Total chunks uploaded: {totalChunks}");
    }

    private async Task<UploadResult> UploadFileAsync(string text, string sourceDocument)
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

        return new UploadResult { ChunkCount = chunks.Count, Ids = ids };
    }

    private class UploadResult
    {
        public int ChunkCount { get; set; }
        public List<int> Ids { get; set; } = new();
    }
}
