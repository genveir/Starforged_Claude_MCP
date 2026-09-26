using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class RequestWritePermissionTool : ITool
{
    private readonly IWritePermissions _writePermissions;
    private readonly ToolGuards _guards;
    private readonly ILogger<RequestWritePermissionTool> _logger;

    public RequestWritePermissionTool(IWritePermissions writePermissions, ToolGuards guards, ILogger<RequestWritePermissionTool> logger)
    {
        _writePermissions = writePermissions;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "request_write_permission",
        Description = "Permits writing in one leaf category: adding, updating, editing sections of, indexing and archiving its documents. Every one of those tools refuses to run until this has been called for the category it is given. The permission covers that category only and lasts until release_write_permission is called for it or the server exits.",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = "Leaf category to permit writes in: " + ToolDescriptions.LeafCategoryRule + " It does not have to exist yet, so that add_document can create it." }
            },
            required = new[] { "category" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        await _guards.RequireLeafCategoryAsync(category);

        _logger.LogInformation("Executing request_write_permission: category={Category}", category);
        _writePermissions.EnableWrite(category);

        return McpJson.Serialize(
            new
            {
                message = $"Writes are now permitted in category '{category}'. " +
                    $"Call release_write_permission for '{category}' as soon as you have finished writing to it."
            });
    }
}
