using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.ConsoleAccess.Cat;

public class DocumentPrinter
{
    private readonly DbInterface dbInterface;

    public DocumentPrinter(DbInterface dbInterface)
    {
        this.dbInterface = dbInterface;
    }

    public async Task Print(CatOptions options)
    {
        var document = await dbInterface.GetDocument(options.Category, options.Filename);

        if (document == null)
        {
            Console.Error.WriteLine($"No document named '{options.Filename}' exists in category '{options.Category}'.");
            return;
        }

        Console.WriteLine("=== Summary ===");
        Console.WriteLine(string.IsNullOrWhiteSpace(document.Summary) ? "(no summary)" : document.Summary);
        Console.WriteLine();
        Console.WriteLine("=== Content ===");
        Console.WriteLine(document.Content);
    }
}
