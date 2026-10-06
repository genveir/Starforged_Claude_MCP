using StarForged_Claude_MCP.Database.DomainTypes;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.ConsoleAccess.List;

public class CategoryLister
{
    private readonly DocumentsRepository documents;

    public CategoryLister(DocumentsRepository documents)
    {
        this.documents = documents;
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
        var categories = await documents.GetCategories();

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

    private async Task ListDocuments(Category category)
    {
        var index = await documents.GetDocumentIndex(category.ToCategoryPath());

        if (index.Count == 0)
        {
            if (!await CategoryHierarchy.RequireLeaf(documents, category)) return;

            Console.Error.WriteLine($"No documents found for category '{category}'.");
            return;
        }

        foreach (var entry in index)
        {
            Console.WriteLine(entry.Filename);
        }
    }
}
