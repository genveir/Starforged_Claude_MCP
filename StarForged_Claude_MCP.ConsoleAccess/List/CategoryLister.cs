using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.ConsoleAccess.List;

public class CategoryLister
{
    private readonly DbInterface dbInterface;

    public CategoryLister(DbInterface dbInterface)
    {
        this.dbInterface = dbInterface;
    }

    public async Task List(ListOptions options)
    {
        if (options.Category == null)
        {
            await ListCategories();
        }
        else
        {
            await ListDocuments(options.Category);
        }
    }

    private async Task ListCategories()
    {
        var categories = await dbInterface.GetCategories();

        if (categories.Count == 0)
        {
            Console.Error.WriteLine("No categories found.");
            return;
        }

        foreach (var category in categories)
        {
            Console.WriteLine(category);
        }
    }

    private async Task ListDocuments(string category)
    {
        var index = await dbInterface.GetDocumentIndex(category);

        if (index.Count == 0)
        {
            Console.Error.WriteLine($"No documents found for category '{category}'.");
            return;
        }

        foreach (var entry in index)
        {
            Console.WriteLine(entry.Filename);
        }
    }
}
