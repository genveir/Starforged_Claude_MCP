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
        var index = await dbInterface.GetDocumentIndex(options.Category);

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
            var path = Path.Combine(options.OutputFolder, SanitiseFileName(entry.Filename));

            if (File.Exists(path) && !options.Overwrite)
            {
                Console.Error.WriteLine($"Skipped (already exists): {path}");
                skipped++;
                continue;
            }

            var document = await dbInterface.GetDocument(options.Category, entry.Filename);
            if (document == null) continue;

            await File.WriteAllTextAsync(path, document.Content);
            Console.WriteLine($"{entry.Filename} -> {path}");
            written++;
        }

        Console.WriteLine($"Exported {written} document(s) from category '{options.Category}' to {options.OutputFolder}.");
        if (skipped > 0)
        {
            Console.WriteLine($"Skipped {skipped} existing file(s); pass --overwrite to replace them.");
        }
    }

    private static string SanitiseFileName(string filename)
    {
        var sanitised = string.Concat(filename.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitised) ? "untitled" : sanitised;
    }
}
