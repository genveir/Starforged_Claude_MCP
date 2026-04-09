using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class GetDocumentsTests(TestFixture fixture) : DocumentsTestBase(fixture)
{
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
}
