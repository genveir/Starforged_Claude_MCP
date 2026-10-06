using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.DocumentLifecycle;

public class AddDocumentTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<AddDocumentTool> _logger;

    public AddDocumentTool(IDocumentsFacade documents, ToolGuards guards, ILogger<AddDocumentTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "add_document",
        Description = "Stores a new document under a filename within a leaf category. A category holds either documents or subcategories, never both, so a new category cannot be created under one that already holds documents. Fails if that category already holds a document with the same filename; use update_document to replace one. Requires that request_write_permission has been called for the category. tag::document-lifecycle",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory + " Categories act as separate namespaces; a new one is created by storing its first document." },
                filename = new { type = "string", description = "Filename, unique within the category (e.g., 'session_5.md')" },
                text = new { type = "string", description = "The full content of the document. " + ToolDescriptions.MarkdownStructure },
                summary = new { type = "string", description = "Optional short summary, shown alongside the filename whenever the category's documents are listed" },
                indexed = new { type = "boolean", description = "Whether to chunk and embed this document so search_index can find it. Use update_document's indexed to change this later." }
            },
            required = new[] { "category", "filename", "text", "indexed" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);
        var text = arguments.RequireString("text", maxLength: 1_000_000);
        var summary = arguments.OptionalStringAllowingEmpty("summary", maxLength: 512);
        var indexed = arguments.RequireBool("indexed");
        _guards.RequireWriteEnabled(category);
        await _guards.RequireLeafCategoryAsync(category);
        await _guards.RequireNoAncestorHoldsDocumentsAsync(category);

        _logger.LogDebug("Executing add_document: category={Category}, filename={Filename}, indexed={Indexed}, textLength={TextLength}",
            category, filename, indexed, text.Length);

        var stored = await _documents.AddDocumentAsync(category, filename, text, summary, indexed);

        if (!stored)
            throw new ArgumentException($"A document named '{filename}' already exists in category '{category}'. Use update_document to replace it.");

        return McpJson.Serialize(new { message = "Document stored successfully" });
    }
}
