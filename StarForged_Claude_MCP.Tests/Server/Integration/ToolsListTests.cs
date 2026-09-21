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
        result.Tools.Select(t => t.Name).Should().BeEquivalentTo(
            "search_index",
            "find_text",
            "retrieve_search_results",
            "add_document",
            "update_document",
            "replace_document_section",
            "append_to_document",
            "delete_document_section",
            "index_document",
            "deindex_document",
            "archive_document",
            "get_document",
            "get_document_summary",
            "document_index",
            "get_canonical_beats",
            "roll_dice",
            "request_write_permission",
            "release_write_permission");
    }

    [Fact]
    public async Task ToolsList_EveryCategoryArgument_ShouldSayWhichLevelOfCategoryItTakes()
    {
        var response = await InvokeServerMethod(new JsonRpcRequest { Id = "4", Method = "tools/list", Params = new { } });
        var tools = JsonSerializer.SerializeToElement(response.Result, _jsonOptions).GetProperty("tools").EnumerateArray();

        var categoryDescriptions = tools
            .Where(tool => tool.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("category", out _))
            .ToDictionary(
                tool => tool.GetProperty("name").GetString()!,
                tool => tool.GetProperty("inputSchema").GetProperty("properties").GetProperty("category")
                    .GetProperty("description").GetString()!);

        categoryDescriptions.Should().NotBeEmpty();

        foreach (var (name, description) in categoryDescriptions)
        {
            description.Should().Contain("dotted path", because: "'{0}' has to show how subcategories are written", name);

            if (name is "search_index" or "find_text")
                description.Should().Contain("any level", because: "'{0}' searches everything under a parent", name);
            else if (name != "release_write_permission")
                description.Should().StartWith("Leaf category", because: "'{0}' refuses a parent category", name);
        }
    }

    [Fact]
    public async Task ToolsList_ShouldNotAdvertiseAWriteToolForBeats()
    {
        var request = new JsonRpcRequest
        {
            Id = "3",
            Method = "tools/list",
            Params = new { }
        };

        var result = JsonSerializer.Deserialize<ToolsListResult>(
            JsonSerializer.Serialize((await InvokeServerMethod(request)).Result, _jsonOptions),
            _jsonOptions);

        result!.Tools.Should().NotContain(t => t.Name.Contains("beat") && t.Name != "get_canonical_beats",
            because: "beats are written by pasting into the console, so that the GM's output never enters context twice");
    }
}
