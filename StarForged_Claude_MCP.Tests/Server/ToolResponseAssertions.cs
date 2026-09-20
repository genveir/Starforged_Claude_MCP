using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Tests.Server;

/// <summary>
/// A refused tool call comes back as a result carrying isError, not as a JSON-RPC error, so that the
/// message reaches the model rather than the client's own wording for protocol failures. Asserting on
/// response.Error would therefore pass for a refusal and a success alike; these go through the result.
/// </summary>
internal static class ToolResponseAssertions
{
    internal static void ShouldHaveSucceeded(this JsonRpcResponse response, string because = "", params object[] becauseArgs)
    {
        response.Error.Should().BeNull(because, becauseArgs);

        var result = response.Result.Should().BeOfType<CallToolResult>(because, becauseArgs).Subject;
        result.IsError.Should().BeFalse(
            because: "the tool refused: {0}", result.Content.FirstOrDefault()?.Text ?? "(no message)");
    }

    /// <summary>Asserts the call was refused and hands back what the caller was told.</summary>
    internal static string ShouldHaveBeenRefused(this JsonRpcResponse response)
    {
        response.Error.Should().BeNull(
            because: "a tool that ran and refused reports it in its result, where the model can read it");

        var result = response.Result.Should().BeOfType<CallToolResult>().Subject;
        result.IsError.Should().BeTrue();
        result.Content.Should().NotBeEmpty(because: "a refusal that explains nothing leaves the caller stuck");

        return result.Content[0].Text;
    }
}
