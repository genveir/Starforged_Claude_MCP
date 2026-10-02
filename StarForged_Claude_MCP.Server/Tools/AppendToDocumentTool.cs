using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class AppendToDocumentTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<AppendToDocumentTool> _logger;

    public AppendToDocumentTool(IDocumentsFacade documents, ToolGuards guards, ILogger<AppendToDocumentTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "append_to_document",
        Description = "Adds text to the end of a document, or to the end of one of its sections, without rewriting what is already there. An indexed document is re-indexed from the result. Requires that request_write_permission has been called for the category. tag::section-editing",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document to append to" },
                section = new { type = "string", description = "Optional. The section to append to, named by its header text without the '#' markers and matched ignoring case; qualify an ambiguous name with headers it sits under, separated by '>', e.g. 'Ironlander Customs > Burial Rites'. The text lands at the very end of that section, after the last subsection nested under it, rather than directly after its own paragraphs. Leave it out to append at the end of the document." },
                text = new { type = "string", description = "The text to add. When appending to a section, any header in it has to be deeper than that section's own header, since one at the same level or shallower would start a new section outside it instead." },
                summary = new { type = "string", description = ToolDescriptions.ReplacementSummary }
            },
            required = new[] { "category", "filename", "text" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);
        var section = arguments.OptionalString("section", maxLength: 1_000);
        var text = arguments.RequireString("text", maxLength: 1_000_000);
        var summary = arguments.OptionalSummary();
        _guards.RequireWriteEnabled(category);

        _logger.LogDebug("Executing append_to_document: category={Category}, filename={Filename}, section={Section}, textLength={TextLength}",
            category, filename, section ?? "(end of document)", text.Length);

        var appended = await _documents.AppendAsync(category, filename, section, text, summary);

        return ToolGuards.RequireDocumentWasFound(appended, category, filename, message: "Text appended successfully");
    }
}
