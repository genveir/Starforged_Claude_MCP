using FluentAssertions;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class RetrieveSearchResultsTests : McpServerTestBase
{
    private const string Category = "lore";

    public RetrieveSearchResultsTests(TestFixture fixture) : base(fixture) =>
        PermitWritesIn(Category);

    [Fact]
    public async Task SearchThenRetrieve_EndToEnd_ShouldYieldFullText()
    {
        await ClearTestDocuments();

        var fullText = "The ancient dragon had guarded its vast golden hoard for centuries. No one had ever dared disturb it.";
        await AddIndexedDocument("dragon.md", fullText);

        var searchResults = await Search("dragon guarding hoard", topK: 1);
        searchResults.Should().ContainSingle();

        var id = searchResults[0].GetProperty("id").GetInt32();

        var retrieved = await Retrieve([id]);

        retrieved.Should().ContainSingle();
        retrieved[0].GetProperty("text").GetString().Should().Be(fullText);
        retrieved[0].GetProperty("filename").GetString().Should().Be("dragon.md");
    }

    [Fact]
    public async Task RetrieveSearchResults_ShouldReturnResultsInTheOrderRequested()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("first.md", "The blacksmith hammered the glowing iron on the anvil.");
        await AddIndexedDocument("second.md", "The spacecraft entered orbit around the red planet.");

        var searchResults = await Search("blacksmith anvil spacecraft orbit", topK: 2);
        searchResults.Should().HaveCount(2);

        var ids = searchResults.Select(r => r.GetProperty("id").GetInt32()).ToArray();
        var reversed = ids.Reverse().ToArray();

        var retrieved = await Retrieve(reversed);

        retrieved.Select(r => r.GetProperty("id").GetInt32()).Should().Equal(reversed);
    }

    [Fact]
    public async Task RetrieveSearchResults_ShouldOmitIdsThatDoNotExist()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("only.md", "The orchestra performed to a standing ovation.");

        var searchResults = await Search("orchestra ovation", topK: 1);
        var realId = searchResults[0].GetProperty("id").GetInt32();

        var retrieved = await Retrieve([realId, 999_999]);

        retrieved.Should().ContainSingle();
        retrieved[0].GetProperty("id").GetInt32().Should().Be(realId);
    }

    [Fact]
    public async Task RetrieveSearchResults_WithNoIds_ShouldReturnError()
    {
        var response = await CallTool("40", "retrieve_search_results", new Dictionary<string, object>
        {
            ["ids"] = Array.Empty<object>()
        });

        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32602);
        response.Error.Message.Should().Contain("cannot be empty");
    }

    private async Task AddIndexedDocument(string filename, string text)
    {
        var response = await CallTool(Guid.NewGuid().ToString(), "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = filename,
            ["text"] = text,
            ["indexed"] = true
        });

        response.Error.Should().BeNull();
    }

    private async Task<JsonElement[]> Search(string query, int topK)
    {
        var response = await CallTool(Guid.NewGuid().ToString(), "search_index", new Dictionary<string, object>
        {
            ["query"] = query,
            ["category"] = Category,
            ["topK"] = topK
        });

        response.Error.Should().BeNull();
        return ToolPayload(response).GetProperty("results").EnumerateArray().ToArray();
    }

    private async Task<JsonElement[]> Retrieve(int[] ids)
    {
        var response = await CallTool(Guid.NewGuid().ToString(), "retrieve_search_results", new Dictionary<string, object>
        {
            ["ids"] = ids.Cast<object>().ToArray()
        });

        response.Error.Should().BeNull();
        return ToolPayload(response).GetProperty("results").EnumerateArray().ToArray();
    }
}
