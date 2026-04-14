using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class DocumentIndexTests(TestFixture fixture) : DocumentsTestBase(fixture)
{
    [Fact]
    public async Task DocumentIndex_ShouldReturnDistinctSourceDocuments()
    {
        await ClearTestDocuments();

        await AddTestDocument("Content A", "alpha");
        await AddTestDocument("Content B", "beta");
        await AddTestDocument("Content C", "alpha");

        var request = new JsonRpcRequest
        {
            Id = "20",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "document_index",
                Arguments = new Dictionary<string, object>()
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("20");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var sourceDocuments = toolResponse.GetProperty("sourceDocuments").EnumerateArray()
            .Select(e => e.GetString())
            .ToArray();

        sourceDocuments.Should().BeEquivalentTo(["alpha", "beta"]);
    }

    [Fact]
    public async Task DocumentIndex_WhenNoDocuments_ShouldReturnEmpty()
    {
        await ClearTestDocuments();

        var request = new JsonRpcRequest
        {
            Id = "21",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "document_index",
                Arguments = new Dictionary<string, object>()
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("21");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var sourceDocuments = toolResponse.GetProperty("sourceDocuments").EnumerateArray().ToArray();

        sourceDocuments.Should().BeEmpty();
    }
}
