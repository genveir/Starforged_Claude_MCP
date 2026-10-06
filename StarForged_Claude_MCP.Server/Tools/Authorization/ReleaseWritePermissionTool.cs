using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Authorization;

public class ReleaseWritePermissionTool : ITool
{
    private readonly IWritePermissions _writePermissions;
    private readonly ILogger<ReleaseWritePermissionTool> _logger;

    public ReleaseWritePermissionTool(IWritePermissions writePermissions, ILogger<ReleaseWritePermissionTool> logger)
    {
        _writePermissions = writePermissions;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "release_write_permission",
        Description = "Revokes the write permission granted by request_write_permission, returning one category to read-only. Succeeds whether or not writes were permitted in it. tag::authorization",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = "The category passed to request_write_permission, as its full dotted path such as 'Campaign.Oracles'" }
            },
            required = new[] { "category" }
        }
    };

    public Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();

        _logger.LogInformation("Executing release_write_permission: category={Category}", category);
        _writePermissions.DisableWrite(category);

        return Task.FromResult(McpJson.Serialize(
            new { message = $"Category '{category}' is now read-only." }));
    }
}
