using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Server.Tools.Abstractions;

/// <summary>
/// One tool the server advertises in tools/list and runs on tools/call. A tool refuses a call it can do
/// nothing with by throwing an <see cref="ArgumentException"/>, whose message is shown to the caller; any
/// other exception is reported as a failure of the server.
/// </summary>
public interface ITool
{
    Tool Definition { get; }

    Task<string> ExecuteAsync(ToolArguments arguments);
}
