using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

/// <summary>
/// Which channel a refusal travels on, checked on the wire rather than on the objects. A refusal sent
/// as a JSON-RPC error is a protocol failure as far as the client is concerned, and clients show the
/// model their own wording for those — so everything these messages say to do went unread.
/// </summary>
public class ToolRefusalTests : McpServerTestBase
{
    private const string Category = "lore";

    public ToolRefusalTests(TestFixture fixture) : base(fixture) => PermitWritesIn(Category);

    [Fact]
    public async Task RefusedToolCall_ShouldTravelAsAnErrorResult_NotAsAJsonRpcError()
    {
        await ClearTestDocuments();

        var response = await CallTool("1", "update_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "never_stored.md",
            ["text"] = "# Nothing to replace"
        });

        var wire = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(response, _jsonOptions));

        wire.TryGetProperty("error", out var error).Should().BeFalse(
            because: "a JSON-RPC error would be rendered by the client, leaving the model nothing to read");
        error.ValueKind.Should().Be(JsonValueKind.Undefined);

        var result = wire.GetProperty("result");
        result.GetProperty("isError").GetBoolean().Should().BeTrue();
        result.GetProperty("content")[0].GetProperty("text").GetString()
            .Should().Contain("No document named", because: "the reason has to reach the caller intact");
    }

    [Fact]
    public async Task RefusedWrite_ShouldTellTheCallerHowToUnblockItself()
    {
        var response = await CallTool("2", "add_document", new Dictionary<string, object>
        {
            ["category"] = "sealed_vault",
            ["filename"] = "forbidden.md",
            ["text"] = "# Forbidden",
            ["indexed"] = false
        });

        response.ShouldHaveBeenRefused().Should().Contain("request_write_permission",
            because: "this is the one message a caller cannot act on without reading it");
    }

    [Fact]
    public async Task SuccessfulToolCall_ShouldNotBeMarkedAsAnError()
    {
        await ClearTestDocuments();

        var response = await CallTool("3", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "stored.md",
            ["text"] = "# Stored",
            ["indexed"] = false
        });

        var wire = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(response, _jsonOptions));

        wire.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UnknownMethod_ShouldStillBeAJsonRpcError()
    {
        var response = await InvokeServerMethod(new JsonRpcRequest
        {
            Id = "4",
            Method = "tools/nonexistent",
            Params = new { }
        });

        response.Error.Should().NotBeNull(
            because: "a method the server cannot make sense of is a protocol failure, not a tool refusing");
        response.Error.Code.Should().Be(-32601);
    }
}
