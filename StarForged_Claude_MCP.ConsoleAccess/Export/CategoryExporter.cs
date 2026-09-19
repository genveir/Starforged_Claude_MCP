using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.ConsoleAccess.Export;

public class CategoryExporter
{
    private readonly DbInterface dbInterface;

    public CategoryExporter(DbInterface dbInterface)
    {
        this.dbInterface = dbInterface;
    }

    public async Task ExportCategory(ExportOptions options)
    {
        var index = await dbInterface.GetDistinctSourceDocuments(options.Category);

        if (index.Count == 0)
        {
            Console.Error.WriteLine($"No documents found for category '{options.Category}'.");
            return;
        }

        Directory.CreateDirectory(options.OutputFolder);

        int written = 0;
        int skipped = 0;

        foreach (var entry in index)
        {
            var path = Path.Combine(options.OutputFolder, $"{SanitiseFileName(entry.SourceDocument)}.md");

            if (File.Exists(path) && !options.Overwrite)
            {
                Console.Error.WriteLine($"Skipped (already exists): {path}");
                skipped++;
                continue;
            }

            var documents = await dbInterface.GetAllDocumentsForSourceDocument(entry.SourceDocument, options.Category);
            var content = string.Join(Environment.NewLine + Environment.NewLine, documents.Select(d => d.Content));

            await File.WriteAllTextAsync(path, content);
            Console.WriteLine($"{entry.SourceDocument} -> {path} ({documents.Count} chunks)");
            written++;
        }

        Console.WriteLine($"Exported {written} document(s) from category '{options.Category}' to {options.OutputFolder}.");
        if (skipped > 0)
        {
            Console.WriteLine($"Skipped {skipped} existing file(s); pass --overwrite to replace them.");
        }
    }

    private static string SanitiseFileName(string sourceDocument)
    {
        var sanitised = string.Concat(sourceDocument.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitised) ? "untitled" : sanitised;
    }
}
