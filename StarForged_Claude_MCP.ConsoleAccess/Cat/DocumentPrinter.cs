using StarForged_Claude_MCP.Database.DomainTypes;
using StarForged_Claude_MCP.Database.Repositories;

namespace StarForged_Claude_MCP.ConsoleAccess.Cat;

public class DocumentPrinter
{
    private readonly DocumentsRepository documents;

    public DocumentPrinter(DocumentsRepository documents)
    {
        this.documents = documents;
    }

    public async Task Print(CatOptions options)
    {
        var document = await documents.GetDocument(options.Category.ToCategoryPath(), options.Filename);

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
