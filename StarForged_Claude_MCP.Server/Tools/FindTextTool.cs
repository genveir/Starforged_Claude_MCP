using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class FindTextTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<FindTextTool> _logger;

    public FindTextTool(IDocumentsFacade documents, ToolGuards guards, ILogger<FindTextTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "find_text",
        Description = "Finds documents containing a case-insensitive word or phrase in a category tree. Use it for names, places, ship names and other terms where exact spelling matters. Searches every document, indexed or not. Returns the matching files, most matches first, each with its leaf category, its match count and up to 5 short snippets labelled with the section they are in; at most 25 files are listed, and truncated says whether more matched. A snippet's section can be passed to the section tools as it is. tag::core-access",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.ScopeCategory },
                text = new { type = "string", description = "The word or phrase to find, matched literally and ignoring case, e.g. 'Bluejay' or 'the Iron Veil'. It is not a pattern: characters such as '*' and '%' match only themselves. Spaces in it also match line breaks." },
                wholeWord = new { type = "boolean", description = "Optional, default false. When true, 'Jay' matches 'Jay' and \"Jay's\" but not 'Bluejay'." },
                filename = new { type = "string", description = "Optional. Search only this document, to see where in it a term appears without fetching the whole file. A filename is only unique within its leaf category, so category then has to be that leaf rather than a parent." }
            },
            required = new[] { "category", "text" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var text = arguments.RequireString("text", maxLength: 200).Trim();
        var wholeWord = arguments.OptionalBool("wholeWord", defaultValue: false);
        var filename = arguments.OptionalString("filename", maxLength: 500);

        if (text.Length < 2)
            throw new ArgumentException("Text has to be at least 2 characters long");

        if (filename != null)
            await _guards.RequireLeafCategoryAsync(category);

        _logger.LogDebug("Executing find_text: category={Category}, text={Text}, wholeWord={WholeWord}, filename={Filename}",
            category, text, wholeWord, filename ?? "(all documents)");

        var result = await _documents.FindTextAsync(category, text, wholeWord, filename);

        if (result == null)
            throw ToolGuards.NoSuchDocument(category, filename!);

        _logger.LogDebug("find_text found {MatchCount} match(es) in {DocumentCount} document(s)",
            result.TotalMatches, result.Documents.Count);
        return McpJson.Serialize(result);
    }
}
