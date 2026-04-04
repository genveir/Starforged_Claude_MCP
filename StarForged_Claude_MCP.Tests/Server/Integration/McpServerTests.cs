using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services.Preprocessing;
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
        content.Text.Should().Contain("results");
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
    public async Task AddMemory_ShouldStoreCorrectTokenCount()
    {
        var testText = "The warrior drew his sword.";

        var request = new JsonRpcRequest
        {
            Id = "8",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "add_memory",
                Arguments = new Dictionary<string, object>
                {
                    { "text", testText },
                    { "sourceDocument", "test_tokencount" }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("8");
        response.Error.Should().BeNull();

        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        var storedResult = await dbInterface.GetTextBySourceDocument("test_tokencount");

        storedResult.Should().NotBeNull();
        var id = storedResult.Single().Id;

        var expectedTokenCount = Tokenizer.Tokenize(testText).Length;

        var tokenCount = await GetTokenCountFromDatabase(id);

        // Verify the token count matches what we calculated
        tokenCount.Should().Be(expectedTokenCount);

        // Verify it's not the old behavior of always returning 512
        tokenCount.Should().NotBe(512);

        // For a short sentence like "The warrior drew his sword.", we expect a small token count
        tokenCount.Should().BeGreaterThan(0, $"token count should be greater than 0, but was {tokenCount}");
        tokenCount.Should().BeLessThanOrEqualTo(512, $"token count should be less than or equal to 512, but was {tokenCount}");
    }

    private async Task<int> GetTokenCountFromDatabase(int id)
    {
        var connectionString = _fixture.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("DefaultConnection");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var tokenCount = await connection.ExecuteScalarAsync<int>(
            "select TokenCount from Embeddings where Id = @Id",
            new { Id = id });

        return tokenCount;
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
