using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class McpServerTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly McpServer _server;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServerTests(TestFixture fixture)
    {
        _fixture = fixture;
        _server = _fixture.Services.GetRequiredService<McpServer>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [Fact]
    public async Task Initialize_ShouldReturnSuccessResponse()
    {
        var request = new JsonRpcRequest
        {
            Id = "1",
            Method = "initialize",
            Params = new { }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("1");
        response.Error.Should().BeNull();
        response.Result.Should().NotBeNull();
    }

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
        result.Tools.Should().HaveCount(2);
        result.Tools.Should().Contain(t => t.Name == "search");
        result.Tools.Should().Contain(t => t.Name == "add_memory");
    }

    [Fact]
    public async Task AddMemory_ShouldStore()
    {
        var request = new JsonRpcRequest
        {
            Id = "3",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "add_memory",
                Arguments = new Dictionary<string, object>
                {
                    { "text", "The ancient dragon sleeps in the mountain." },
                    { "sourceDocument", "test_campaign" }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("3");
        response.Error.Should().BeNull();

        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        var storedResult = await dbInterface.GetTextBySourceDocument("test_campaign");

        storedResult.Should().NotBeNull();
        storedResult.Should().HaveCount(1);

        storedResult.Single().Text.Should().Be("The ancient dragon sleeps in the mountain.");
    }

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
                Name = "search",
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
            .Select(e => e.GetString())
            .ToArray();

        results.Should().Contain("The wizard cast a powerful fireball spell.");
        results.Should().Contain("The rogue snuck past the guards silently.");
    }

    [Fact]
    public async Task AddMemory_WithEmptyText_ShouldReturnError()
    {
        var request = new JsonRpcRequest
        {
            Id = "5",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "add_memory",
                Arguments = new Dictionary<string, object>
                {
                    { "text", "" },
                    { "sourceDocument", "test" }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("5");
        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32602);
        response.Error.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task UnknownMethod_ShouldReturnMethodNotFound()
    {
        var request = new JsonRpcRequest
        {
            Id = "6",
            Method = "unknown/method",
            Params = new { }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("6");
        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32601);
        response.Error.Message.Should().Contain("Method not found");
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
                Name = "search",
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
            .Select(e => e.GetString())
            .ToArray();

        results.Should().HaveCount(2);
        results.Should().Contain("The coral reef teems with colorful tropical fish and sea anemones.");
        results.Should().Contain("Dolphins are highly intelligent marine mammals that live in the ocean.");
    }

    private async Task ClearTestMemories()
    {
        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        await dbInterface.DeleteAllEmbeddings();
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

    private async Task<JsonRpcResponse> InvokeServerMethod(JsonRpcRequest request)
    {
        var handleRequestMethod = typeof(McpServer)
            .GetMethod("HandleRequestAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task<JsonRpcResponse>)handleRequestMethod!.Invoke(_server, [request])!;
        return await task;
    }
}
