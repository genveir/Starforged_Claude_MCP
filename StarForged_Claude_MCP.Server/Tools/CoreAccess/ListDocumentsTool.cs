using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.CoreAccess;

public class ListDocumentsTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<ListDocumentsTool> _logger;

    public ListDocumentsTool(IDocumentsFacade documents, ToolGuards guards, ILogger<ListDocumentsTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "list_documents",
        Description = "Lists all documents in a leaf category with their summaries, filenames and whether they are indexed, but not their content. Use it to browse a category, see which documents exist, or find a filename. Use get_document to fetch one in full. Given a parent category, it is refused with a list of the leaf categories under it. tag::core-access",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = "Leaf category to list: " + ToolDescriptions.LeafCategoryRule }
            },
            required = new[] { "category" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        await _guards.RequireLeafCategoryAsync(category);

        _logger.LogDebug("Executing list_documents: category={Category}", category);
        var documents = await _documents.GetDocumentIndexAsync(category);
        _logger.LogDebug("list_documents returned {Count} document(s)", documents.Count);
        return McpJson.Serialize(new { documents });
    }
}
