using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class AddDocumentTests(TestFixture fixture) : DocumentsTestBase(fixture)
{
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
}
