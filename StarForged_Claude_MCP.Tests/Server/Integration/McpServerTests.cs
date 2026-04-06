using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services;
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
        result.Tools.Should().HaveCount(5);
        result.Tools.Should().Contain(t => t.Name == "search");
        result.Tools.Should().Contain(t => t.Name == "add_searchable");
        result.Tools.Should().Contain(t => t.Name == "add_document");
        result.Tools.Should().Contain(t => t.Name == "get_documents");
        result.Tools.Should().Contain(t => t.Name == "get_canonical_beats");
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

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var ids = toolResponse.GetProperty("id").EnumerateArray().Select(e => e.GetInt32()).ToArray();

        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        var storedResult = await dbInterface.GetEmbeddedTextByIds(ids);

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
            .Select(e => e.GetProperty("text").GetString())
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
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();

        results.Should().HaveCount(2);
        results.Should().Contain("The coral reef teems with colorful tropical fish and sea anemones.");
        results.Should().Contain("Dolphins are highly intelligent marine mammals that live in the ocean.");
    }

    private async Task ClearTestMemories()
    {
        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        await dbInterface.DeleteAllEmbeddings();

        var vectorCache = _fixture.Services.GetRequiredService<VectorCacheService>();
        await vectorCache.RefreshCache();
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
    public async Task AddDocument_ShouldStore()
    {
        await ClearTestDocuments();

        var request = new JsonRpcRequest
        {
            Id = "8",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "add_document",
                Arguments = new Dictionary<string, object>
                {
                    { "text", "The hero entered the tavern at midnight." },
                    { "sourceDocument", "test_doc_store" }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("8");
        response.Error.Should().BeNull();

        var getRequest = new JsonRpcRequest
        {
            Id = "9",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "get_documents",
                Arguments = new Dictionary<string, object>
                {
                    { "sourceDocument", "test_doc_store" }
                }
            }
        };

        var getResponse = await InvokeServerMethod(getRequest);

        getResponse.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(getResponse.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var documents = toolResponse.GetProperty("documents").EnumerateArray().ToArray();

        documents.Should().HaveCount(1);
        documents[0].GetProperty("content").GetString().Should().Be("The hero entered the tavern at midnight.");
    }

    [Fact]
    public async Task GetDocuments_ShouldReturnInOrder()
    {
        await ClearTestDocuments();

        await AddTestDocument("First session: the party met in Ironhaven.", "test_doc_order");
        await AddTestDocument("Second session: the party travelled to the forest.", "test_doc_order");
        await AddTestDocument("Third session: the dragon was defeated.", "test_doc_order");

        var request = new JsonRpcRequest
        {
            Id = "10",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "get_documents",
                Arguments = new Dictionary<string, object>
                {
                    { "sourceDocument", "test_doc_order" }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("10");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var documents = toolResponse.GetProperty("documents").EnumerateArray().ToArray();

        documents.Should().HaveCount(3);
        documents[0].GetProperty("content").GetString().Should().Be("First session: the party met in Ironhaven.");
        documents[0].GetProperty("sequence").GetInt32().Should().Be(1);
        documents[1].GetProperty("content").GetString().Should().Be("Second session: the party travelled to the forest.");
        documents[1].GetProperty("sequence").GetInt32().Should().Be(2);
        documents[2].GetProperty("content").GetString().Should().Be("Third session: the dragon was defeated.");
        documents[2].GetProperty("sequence").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task GetCanonicalBeats_ShouldReturnBeatsForSession()
    {
        await ClearTestDocuments();

        await AddTestDocument("Prologue: the party received a mysterious invitation.", "SessionBeats_5", null);
        await AddTestDocument("The party arrived in the city of Ironhaven.", "SessionBeats_5", "1.0");
        await AddTestDocument("They discovered the merchant was a spy.", "SessionBeats_5", "2.0");
        await AddTestDocument("They discovered the butcher was a spy.", "SessionBeats_5", "2.1");
        await AddTestDocument("Meanwhile, the rival adventuring party plotted in the shadows.", "SessionBeats_5", null);
        await AddTestDocument("A chase through the market ended with an arrest.", "SessionBeats_5", "3.0");

        var request = new JsonRpcRequest
        {
            Id = "11",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "get_canonical_beats",
                Arguments = new Dictionary<string, object>
                {
                    { "sessionNumber", 5 }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("11");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var documents = toolResponse.GetProperty("documents").EnumerateArray().ToArray();

        documents.Should().HaveCount(5);
        checkDocument(documents[0], "Prologue: the party received a mysterious invitation.", 1, null);
        checkDocument(documents[1], "The party arrived in the city of Ironhaven.", 2, "1.0");
        checkDocument(documents[2], "They discovered the butcher was a spy.", 3, "2.1");
        checkDocument(documents[3], "Meanwhile, the rival adventuring party plotted in the shadows.", 4, null);
        checkDocument(documents[4], "A chase through the market ended with an arrest.", 5, "3.0");

        void checkDocument(JsonElement doc, string expectedContent, int expectedSequence, string? expectedBeatNumber)
        {
            doc.GetProperty("content").GetString().Should().Be(expectedContent);
            doc.GetProperty("sequence").GetInt32().Should().Be(expectedSequence);
            if (expectedBeatNumber == null)
            {
                doc.TryGetProperty("beatNumber", out var beatNumberProp).Should().BeFalse();
            }
            else
            {
                doc.GetProperty("beatNumber").GetString().Should().Be(expectedBeatNumber);
            }
        }
    }

    private async Task ClearTestDocuments()
    {
        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        await dbInterface.DeleteAllDocuments();
    }

    private async Task AddTestDocument(string text, string sourceDocument, string? beatNumber = null)
    {
        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        await dbInterface.StoreDocument(text, sourceDocument, beatNumber);
    }

    private async Task<JsonRpcResponse> InvokeServerMethod(JsonRpcRequest request)
    {
        var handleRequestMethod = typeof(McpServer)
            .GetMethod("HandleRequestAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task<JsonRpcResponse>)handleRequestMethod!.Invoke(_server, [request])!;
        return await task;
    }
}
