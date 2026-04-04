using StarForged_Claude_MCP.Embeddings.Services;

namespace StarForged_Claude_MCP.DirectUpload;

public class FileUploader
{
    private readonly IDocumentProcessingService documentProcessingService;

    public FileUploader(
        IDocumentProcessingService documentProcessingService)
    {
        this.documentProcessingService = documentProcessingService;
    }

    public async Task UploadFolderAsync(string folderPath)
    {
        var files = Directory.GetFiles(folderPath, "*.md", SearchOption.AllDirectories);

        Console.WriteLine($"Found {files.Length} file(s) to process.");

        var totalChunks = 0;

        foreach (var filePath in files)
        {

            Console.WriteLine($"Processing: {filePath}");

            var text = await File.ReadAllTextAsync(filePath);

            var fileName = Path.GetFileName(filePath);

            var result = await UploadFileAsync(text, fileName);

            Console.WriteLine($"  Uploaded {result.ChunkCount} chunk(s) with IDs: {string.Join(", ", result.Ids)}");
            totalChunks += result.ChunkCount;
        }

        Console.WriteLine($"\nCompleted! Total chunks uploaded: {totalChunks}");
    }

    private async Task<UploadResult> UploadFileAsync(string text, string sourceDocument)
    {
        var ids = await documentProcessingService.ProcessAndStoreDocumentAsync(text, sourceDocument, DocumentProcessorToUse.Markdown);

        return new UploadResult { ChunkCount = ids.Length, Ids = ids };
    }

    private class UploadResult
    {
        public int ChunkCount { get; set; }
        public int[] Ids { get; set; } = [];
    }
}
