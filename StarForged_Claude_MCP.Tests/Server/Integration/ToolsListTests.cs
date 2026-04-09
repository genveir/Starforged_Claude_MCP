using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class ToolsListTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    [Fact]
    public async Task ToolsList_ShouldReturnAllTools()
    {
        var request = new JsonRpcRequest
        {
            Id = "2",
            Method = "tools/list",
            Params = new { }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("2");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<ToolsListResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        result.Should().NotBeNull();
        result.Tools.Should().NotBeNull();
        result.Tools.Should().HaveCount(6);
        result.Tools.Should().Contain(t => t.Name == "search_index");
        result.Tools.Should().Contain(t => t.Name == "add_searchable");
        result.Tools.Should().Contain(t => t.Name == "add_document");
        result.Tools.Should().Contain(t => t.Name == "get_documents");
        result.Tools.Should().Contain(t => t.Name == "get_canonical_beats");
        result.Tools.Should().Contain(t => t.Name == "retrieve_search_results");
    }
}
