using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class GetDocumentTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<GetDocumentTool> _logger;

    public GetDocumentTool(IDocumentsFacade documents, ToolGuards guards, ILogger<GetDocumentTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "get_document",
        Description = "Retrieves one document in full by category and filename, as listed by list_documents.",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document to retrieve" }
            },
            required = new[] { "category", "filename" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);

        await _guards.RequireLeafCategoryAsync(category);

        _logger.LogDebug("Executing get_document: category={Category}, filename={Filename}", category, filename);

        var document = await _documents.GetDocumentAsync(category, filename);

        if (document == null)
            throw ToolGuards.NoSuchDocument(category, filename);

        return McpJson.Serialize(new { document });
    }
}
