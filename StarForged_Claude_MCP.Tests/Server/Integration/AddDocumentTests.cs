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

    [Fact]
    public async Task AddDocument_WithSummaryAndCategory_ShouldRoundTrip()
    {
        await ClearTestDocuments();

        var request = new JsonRpcRequest
        {
            Id = "10",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "add_document",
                Arguments = new Dictionary<string, object>
                {
                    { "text", "The dragon descended on the village at dawn." },
                    { "sourceDocument", "test_doc_store" },
                    { "summary", "Dragon attack" },
                    { "category", "combat" }
                }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var getRequest = new JsonRpcRequest
        {
            Id = "11",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "get_documents",
                Arguments = new Dictionary<string, object>
                {
                    { "sourceDocument", "test_doc_store" },
                    { "category", "combat" }
                }
            }
        };

        var getResponse = await InvokeServerMethod(getRequest);
        getResponse.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(getResponse.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var doc = toolResponse.GetProperty("documents").EnumerateArray().Single();

        doc.GetProperty("content").GetString().Should().Be("The dragon descended on the village at dawn.");
        doc.GetProperty("summary").GetString().Should().Be("Dragon attack");
        doc.GetProperty("category").GetString().Should().Be("combat");
    }

    [Fact]
    public async Task GetDocuments_WithCategoryFilter_ShouldReturnOnlyMatchingDocuments()
    {
        await ClearTestDocuments();

        await AddTestDocument("The rogue picked the lock.", "test_doc_store", category: "stealth");
        await AddTestDocument("The paladin smote the undead.", "test_doc_store", category: "combat");
        await AddTestDocument("The wizard cast a fireball.", "test_doc_store", category: "combat");

        var request = new JsonRpcRequest
        {
            Id = "12",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "get_documents",
                Arguments = new Dictionary<string, object>
                {
                    { "sourceDocument", "test_doc_store" },
                    { "category", "combat" }
                }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var documents = toolResponse.GetProperty("documents").EnumerateArray().ToArray();

        documents.Should().HaveCount(2);
        documents[0].GetProperty("content").GetString().Should().Be("The paladin smote the undead.");
        documents[1].GetProperty("content").GetString().Should().Be("The wizard cast a fireball.");
    }

    [Fact]
    public async Task GetDocuments_WithNoCategory_ShouldReturnOnlyNullCategoryDocuments()
    {
        await ClearTestDocuments();

        await AddTestDocument("The bard sang a ballad.", "test_doc_store");
        await AddTestDocument("The rogue picked the lock.", "test_doc_store", category: "stealth");
        await AddTestDocument("The cleric prayed for guidance.", "test_doc_store");

        var request = new JsonRpcRequest
        {
            Id = "13",
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

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var documents = toolResponse.GetProperty("documents").EnumerateArray().ToArray();

        documents.Should().HaveCount(2);
        documents[0].GetProperty("content").GetString().Should().Be("The bard sang a ballad.");
        documents[1].GetProperty("content").GetString().Should().Be("The cleric prayed for guidance.");
    }
}
