using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class DocumentIndexTests(TestFixture fixture) : DocumentsTestBase(fixture)
{
    [Fact]
    public async Task DocumentIndex_ShouldReturnDistinctSourceDocuments()
    {
        await ClearTestDocuments();

        await AddTestDocument("Content A", "alpha");
        await AddTestDocument("Content B", "beta");
        await AddTestDocument("Content C", "alpha");

        var request = new JsonRpcRequest
        {
            Id = "20",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "document_index",
                Arguments = new Dictionary<string, object>()
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("20");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var sourceDocuments = toolResponse.GetProperty("sourceDocuments").EnumerateArray()
            .Select(e => e.GetProperty("sourceDocument").GetString())
            .ToArray();

        sourceDocuments.Should().BeEquivalentTo(["alpha", "beta"]);
    }

    [Fact]
    public async Task DocumentIndex_WhenNoDocuments_ShouldReturnEmpty()
    {
        await ClearTestDocuments();

        var request = new JsonRpcRequest
        {
            Id = "21",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "document_index",
                Arguments = new Dictionary<string, object>()
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("21");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var sourceDocuments = toolResponse.GetProperty("sourceDocuments").EnumerateArray().ToArray();

        sourceDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task DocumentIndex_WithCategoryFilter_ShouldReturnOnlyMatchingSourceDocuments()
    {
        await ClearTestDocuments();

        await AddTestDocument("Content A", "alpha", category: "combat");
        await AddTestDocument("Content B", "beta", category: "intrigue");
        await AddTestDocument("Content C", "gamma", category: "combat");
        await AddTestDocument("Content D", "gamma", category: "intrigue");

        var request = new JsonRpcRequest
        {
            Id = "23",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "document_index",
                Arguments = new Dictionary<string, object> { { "category", "combat" } }
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var sourceDocuments = toolResponse.GetProperty("sourceDocuments").EnumerateArray()
            .Select(e => e.GetProperty("sourceDocument").GetString())
            .ToArray();

        sourceDocuments.Should().BeEquivalentTo(["alpha", "gamma"]);
    }

    [Fact]
    public async Task DocumentIndex_ShouldAggregateSummaries()
    {
        await ClearTestDocuments();

        await AddTestDocument("Content A", "alpha", summary: "First summary");
        await AddTestDocument("Content B", "alpha", summary: "Second summary");
        await AddTestDocument("Content C", "alpha", summary: "First summary");
        await AddTestDocument("Content D", "beta", summary: "Only summary");
        await AddTestDocument("Content E", "gamma");

        var request = new JsonRpcRequest
        {
            Id = "22",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "document_index",
                Arguments = new Dictionary<string, object>()
            }
        };

        var response = await InvokeServerMethod(request);
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var entries = toolResponse.GetProperty("sourceDocuments").EnumerateArray().ToArray();

        entries.Should().HaveCount(3);

        var alpha = entries.Single(e => e.GetProperty("sourceDocument").GetString() == "alpha");
        var alphaSummaries = alpha.GetProperty("summaries").GetString()!;
        alphaSummaries.Split(", ").Should().BeEquivalentTo(["First summary", "Second summary"]);

        var beta = entries.Single(e => e.GetProperty("sourceDocument").GetString() == "beta");
        beta.GetProperty("summaries").GetString().Should().Be("Only summary");

        var gamma = entries.Single(e => e.GetProperty("sourceDocument").GetString() == "gamma");
        gamma.TryGetProperty("summaries", out _).Should().BeFalse();
    }
}
