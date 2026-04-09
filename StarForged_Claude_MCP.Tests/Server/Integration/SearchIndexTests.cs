using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class SearchIndexTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    [Fact]
    public async Task Search_WithValidQuery_ShouldReturnResults()
    {
        await ClearTestMemories();

        await AddTestMemory("The wizard cast a powerful fireball spell.", "test_search");
        await AddTestMemory("The rogue snuck past the guards silently.", "test_search");

        var request = new JsonRpcRequest
        {
            Id = "4",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "search_index",
                Arguments = new Dictionary<string, object>
                {
                    { "query", "magic spells" },
                    { "topK", 2 }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("4");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        result.Should().NotBeNull();
        result.Content.Should().NotBeNull();
        result.Content.Should().HaveCount(1);

        var content = result.Content[0];
        content.Text.Should().NotBeNull();

        var searchResponse = JsonSerializer.Deserialize<JsonElement>(content.Text, _jsonOptions);
        var results = searchResponse.GetProperty("results")
            .EnumerateArray()
            .Select(e => e.GetProperty("summary").GetString())
            .ToArray();

        results.Should().Contain("The wizard cast a powerful fireball spell.");
        results.Should().Contain("The rogue snuck past the guards silently.");
    }

    [Fact]
    public async Task Search_SemanticRelevance_ShouldReturnTopTwoRelatedEntries()
    {
        var sourceDoc = "test_semantic_relevance";

        await ClearTestMemories();

        await AddTestMemory("The blacksmith hammered the glowing iron on the anvil.", sourceDoc);
        await AddTestMemory("She baked a sourdough loaf with rosemary and sea salt.", sourceDoc);
        await AddTestMemory("The spacecraft entered orbit around the red planet.", sourceDoc);
        await AddTestMemory("A dense fog rolled over the mountain peaks at dawn.", sourceDoc);
        await AddTestMemory("The chess grandmaster sacrificed his queen to secure the endgame.", sourceDoc);

        await AddTestMemory("The coral reef teems with colorful tropical fish and sea anemones.", sourceDoc);
        await AddTestMemory("Dolphins are highly intelligent marine mammals that live in the ocean.", sourceDoc);

        await AddTestMemory("Lightning struck the old oak tree at the edge of the field.", sourceDoc);
        await AddTestMemory("The orchestra performed Beethoven's Fifth Symphony to a standing ovation.", sourceDoc);
        await AddTestMemory("He debugged the memory leak by profiling heap allocations.", sourceDoc);
        await AddTestMemory("The tax reform bill passed through the senate with a narrow majority.", sourceDoc);
        await AddTestMemory("Ancient Roman aqueducts supplied fresh water to cities across the empire.", sourceDoc);

        var request = new JsonRpcRequest
        {
            Id = "7",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "search_index",
                Arguments = new Dictionary<string, object>
                {
                    { "query", "ocean wildlife and sea creatures" },
                    { "topK", 2 }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("7");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        result.Should().NotBeNull();
        result.Content.Should().NotBeNull();
        result.Content.Should().HaveCount(1);

        var content = result.Content[0];
        content.Text.Should().NotBeNull();

        var searchResponse = JsonSerializer.Deserialize<JsonElement>(content.Text, _jsonOptions);
        var results = searchResponse.GetProperty("results")
            .EnumerateArray()
            .Select(e => e.GetProperty("summary").GetString())
            .ToArray();

        results.Should().HaveCount(2);
        results.Should().Contain("The coral reef teems with colorful tropical fish and sea anemones.");
        results.Should().Contain("Dolphins are highly intelligent marine mammals that live in the ocean.");
    }

    private async Task AddTestMemory(string text, string sourceDocument)
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
    }

    [Fact]
    public async Task SearchIndex_ShortFirstSentence_ShouldReturnFirstTwoSentences()
    {
        await ClearTestMemories();

        var fullText = "The dragon slept. It had guarded its vast golden hoard for centuries. No one had ever dared disturb it.";
        await AddTestMemory(fullText, "test_summary_short");

        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "search_index",
                Arguments = new Dictionary<string, object>
                {
                    { "query", "sleeping dragon" },
                    { "topK", 1 }
                }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
        var searchResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var summary = searchResponse.GetProperty("results").EnumerateArray().First().GetProperty("summary").GetString();

        summary.Should().Be("The dragon slept. It had guarded its vast golden hoard for centuries.");
        summary.Should().NotContain("No one had ever dared disturb it.");
    }

    [Fact]
    public async Task SearchIndex_LongFirstSentence_ShouldReturnFirstSentenceOnly()
    {
        await ClearTestMemories();

        var fullText = "The ancient dragon had guarded its vast golden hoard for centuries. No one had ever dared disturb it.";
        await AddTestMemory(fullText, "test_summary_long");

        var request = new JsonRpcRequest
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

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
        var searchResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var summary = searchResponse.GetProperty("results").EnumerateArray().First().GetProperty("summary").GetString();

        summary.Should().Be("The ancient dragon had guarded its vast golden hoard for centuries.");
        summary.Should().NotContain("No one had ever dared disturb it.");
    }

    [Fact]
    public async Task SearchIndex_BreadcrumbText_ShouldPreservePrefixInSummary()
    {
        await ClearTestMemories();

        var fullText = "Dragon's Lair: The ancient dragon had guarded its vast golden hoard for centuries. No one had ever dared disturb it.";
        await AddTestMemory(fullText, "test_summary_breadcrumb");

        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "search_index",
                Arguments = new Dictionary<string, object>
                {
                    { "query", "dragon lair hoard" },
                    { "topK", 1 }
                }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
        var searchResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var summary = searchResponse.GetProperty("results").EnumerateArray().First().GetProperty("summary").GetString();

        summary.Should().Be("Dragon's Lair: The ancient dragon had guarded its vast golden hoard for centuries.");
        summary.Should().NotContain("No one had ever dared disturb it.");
    }
}
