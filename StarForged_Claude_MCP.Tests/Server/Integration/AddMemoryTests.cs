using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class AddMemoryTests(TestFixture fixture) : McpServerTestBase(fixture)
{
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
    public async Task AddMemory_WhenAddedTwice_ShouldReturnSameIdAndStoreOnce()
    {
        await ClearTestMemories();

        var makeRequest = (string id) => new JsonRpcRequest
        {
            Id = id,
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "add_memory",
                Arguments = new Dictionary<string, object>
                {
                    { "text", "The ancient dragon sleeps in the mountain." },
                    { "sourceDocument", "test_dedup" }
                }
            }
        };

        int[] ParseIds(JsonRpcResponse response)
        {
            var result = JsonSerializer.Deserialize<CallToolResult>(
                JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
            return JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions)
                .GetProperty("id").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        }

        var ids1 = ParseIds(await InvokeServerMethod(makeRequest("10")));
        var ids2 = ParseIds(await InvokeServerMethod(makeRequest("11")));

        ids1.Should().NotBeEmpty();
        ids2.Should().Equal(ids1);

        var connectionString = _fixture.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("DefaultConnection")!;
        await using var connection = new SqlConnection(connectionString);
        var count = await connection.ExecuteScalarAsync<int>(
            "select count(*) from Embeddings where SourceDocument = 'test_dedup'");
        count.Should().Be(ids1.Length);
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
}
