using FluentAssertions;
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
