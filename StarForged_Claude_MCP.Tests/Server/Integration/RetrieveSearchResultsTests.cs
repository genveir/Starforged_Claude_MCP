using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class RetrieveSearchResultsTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    [Fact]
    public async Task RetrieveSearchResults_ShouldReturnFullText()
    {
        var fullText = "The ancient dragon had guarded its vast golden hoard for centuries. No one had ever dared disturb it.";
        var ids = await AddTestMemoryAndGetIds(fullText, "test_retrieve_full");

        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "retrieve_search_results",
                Arguments = new Dictionary<string, object>
                {
                    { "ids", ids.Cast<object>().ToArray() }
                }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var results = toolResponse.GetProperty("results").EnumerateArray().ToArray();

        results.Should().HaveCount(1);
        results[0].GetProperty("text").GetString().Should().Be(fullText);
    }

    [Fact]
    public async Task SearchThenRetrieve_EndToEnd_ShouldYieldFullText()
    {
        await ClearTestMemories();

        var fullText = "The ancient dragon had guarded its vast golden hoard for centuries. No one had ever dared disturb it.";
        await AddTestMemory(fullText, "test_retrieve_e2e");

        var searchRequest = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "search_index",
                Arguments = new Dictionary<string, object>
                {
                    { "query", "dragon guarding hoard" },
                    { "topK", 1 }
                }
            }
        };

        var searchResponse = await InvokeServerMethod(searchRequest);
        searchResponse.Error.Should().BeNull();

        var searchResult = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(searchResponse.Result, _jsonOptions), _jsonOptions);
        var searchJson = JsonSerializer.Deserialize<JsonElement>(searchResult!.Content[0].Text, _jsonOptions);
        var ids = searchJson.GetProperty("results")
            .EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32())
            .Cast<object>()
            .ToArray();

        var retrieveRequest = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "retrieve_search_results",
                Arguments = new Dictionary<string, object>
                {
                    { "ids", ids }
                }
            }
        };

        var retrieveResponse = await InvokeServerMethod(retrieveRequest);
        retrieveResponse.Error.Should().BeNull();

        var retrieveResult = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(retrieveResponse.Result, _jsonOptions), _jsonOptions);
        var retrieveJson = JsonSerializer.Deserialize<JsonElement>(retrieveResult!.Content[0].Text, _jsonOptions);
        var texts = retrieveJson.GetProperty("results")
            .EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();

        texts.Should().Contain(fullText);
    }

    [Fact]
    public async Task RetrieveSearchResults_ShouldHonourRequestedIdOrder()
    {
        var idsA = await AddTestMemoryAndGetIds("The blacksmith hammered iron at the forge.", "test_retrieve_order");
        var idsB = await AddTestMemoryAndGetIds("The baker kneaded dough in the kitchen.", "test_retrieve_order");

        var idA = idsA[0];
        var idB = idsB[0];

        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "retrieve_search_results",
                Arguments = new Dictionary<string, object>
                {
                    { "ids", new object[] { idB, idA } }
                }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var results = toolResponse.GetProperty("results").EnumerateArray().ToArray();

        results.Should().HaveCount(2);
        results[0].GetProperty("text").GetString().Should().Be("The baker kneaded dough in the kitchen.");
        results[1].GetProperty("text").GetString().Should().Be("The blacksmith hammered iron at the forge.");
    }

    [Fact]
    public async Task RetrieveSearchResults_ShouldOmitUnknownIds()
    {
        var ids = await AddTestMemoryAndGetIds("The scout reported enemy movements at the border.", "test_retrieve_omit");
        var knownId = ids[0];

        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "retrieve_search_results",
                Arguments = new Dictionary<string, object>
                {
                    { "ids", new object[] { knownId, 999999 } }
                }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var results = toolResponse.GetProperty("results").EnumerateArray().ToArray();

        results.Should().HaveCount(1);
        results[0].GetProperty("text").GetString().Should().Be("The scout reported enemy movements at the border.");
    }

    private async Task AddTestMemory(string text, string sourceDocument)
    {
        await AddTestMemoryAndGetIds(text, sourceDocument);
    }

    private async Task<int[]> AddTestMemoryAndGetIds(string text, string sourceDocument)
    {
        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "add_memory",
                Arguments = new Dictionary<string, object>
                {
                    { "text", text },
                    { "sourceDocument", sourceDocument }
                }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        return toolResponse.GetProperty("id").EnumerateArray().Select(e => e.GetInt32()).ToArray();
    }
}
