using FluentAssertions;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class CategoryHierarchyTests : McpServerTestBase
{
    private const string Parent = "Campaign";
    private const string Oracles = "Campaign.Oracles";
    private const string Sessions = "Campaign.Sessions";

    public CategoryHierarchyTests(TestFixture fixture) : base(fixture) =>
        PermitWritesIn(Oracles, Sessions, "CampaignX.Notes", "AxB.Notes", "A_B.Notes");

    [Fact]
    public async Task Search_OnAParentCategory_ShouldSearchEveryCategoryUnderIt()
    {
        await ClearTestDocuments();
        await AddDocument(Oracles, "moons.md", "The twin moons rise over the Forge.", indexed: true);
        await AddDocument(Sessions, "session_1.md", "The crew watched the moons rise from the hull.", indexed: true);
        await AddDocument("CampaignX.Notes", "moons.md", "The moons of another campaign entirely.", indexed: true);

        var results = await SearchResults("moons rising", category: Parent);

        results.Select(r => (r.GetProperty("category").GetString(), r.GetProperty("filename").GetString()))
            .Should().BeEquivalentTo(new[] { (Oracles, "moons.md"), (Sessions, "session_1.md") },
                because: "'CampaignX' starts with the same letters but is not under 'Campaign'");
    }

    [Fact]
    public async Task Search_OnALeafCategory_ShouldSearchOnlyThatCategory()
    {
        await ClearTestDocuments();
        await AddDocument(Oracles, "moons.md", "The twin moons rise over the Forge.", indexed: true);
        await AddDocument(Sessions, "session_1.md", "The crew watched the moons rise from the hull.", indexed: true);

        var results = await SearchResults("moons rising", category: Oracles);

        results.Select(r => r.GetProperty("category").GetString()).Should().AllBe(Oracles);
    }

    [Fact]
    public async Task Search_ShouldNotTreatAnUnderscoreInTheCategoryAsAWildcard()
    {
        await ClearTestDocuments();
        await AddDocument("AxB.Notes", "moons.md", "The twin moons rise over the Forge.", indexed: true);
        await AddDocument("A_B.Notes", "moons.md", "The moons rise again.", indexed: true);

        var results = await SearchResults("moons rising", category: "A_B");

        results.Select(r => r.GetProperty("category").GetString()).Should().AllBe("A_B.Notes");
    }

    [Fact]
    public async Task FindText_OnAParentCategory_ShouldSearchEveryCategoryUnderIt()
    {
        await ClearTestDocuments();
        await AddDocument(Oracles, "moons.md", "# Moons\n\nBluejay named them.");
        await AddDocument(Sessions, "session_1.md", "# Session 1\n\nBluejay took the helm.");
        await AddDocument("CampaignX.Notes", "notes.md", "# Notes\n\nA different Bluejay.");

        var response = await CallTool("1", "find_text", new Dictionary<string, object>
        {
            ["category"] = Parent,
            ["text"] = "bluejay"
        });

        response.ShouldHaveSucceeded();
        ToolPayload(response).GetProperty("documents").EnumerateArray()
            .Select(d => (d.GetProperty("category").GetString(), d.GetProperty("filename").GetString()))
            .Should().BeEquivalentTo(new[] { (Oracles, "moons.md"), (Sessions, "session_1.md") });
    }

    [Fact]
    public async Task FindText_WithAFilenameOnAParentCategory_ShouldBeRefusedWithTheLeavesUnderIt()
    {
        await ClearTestDocuments();
        await AddDocument(Oracles, "moons.md", "# Moons\n\nBluejay named them.");
        await AddDocument(Sessions, "session_1.md", "# Session 1\n\nBluejay took the helm.");

        var response = await CallTool("2", "find_text", new Dictionary<string, object>
        {
            ["category"] = Parent,
            ["text"] = "bluejay",
            ["filename"] = "moons.md"
        });

        response.ShouldHaveBeenRefused().Should().Contain("parent category").And.Contain(Oracles).And.Contain(Sessions);
    }

    [Theory]
    [InlineData("document_index")]
    [InlineData("get_document")]
    [InlineData("get_document_summary")]
    [InlineData("get_canonical_beats")]
    [InlineData("request_write_permission")]
    public async Task LeafTool_OnAParentCategory_ShouldBeRefusedWithTheLeavesUnderIt(string toolName)
    {
        await ClearTestDocuments();
        await AddDocument(Oracles, "moons.md", "# Moons");
        await AddDocument(Sessions, "session_1.md", "# Session 1");

        var response = await CallTool("3", toolName, new Dictionary<string, object>
        {
            ["category"] = Parent,
            ["filename"] = "moons.md",
            ["sessionNumber"] = 1
        });

        response.ShouldHaveBeenRefused().Should().Contain("parent category").And.Contain(Oracles).And.Contain(Sessions);
    }

    [Fact]
    public async Task DocumentIndex_OnALeafCategory_ShouldListOnlyItsOwnDocuments()
    {
        await ClearTestDocuments();
        await AddDocument(Oracles, "moons.md", "# Moons");
        await AddDocument(Sessions, "session_1.md", "# Session 1");

        var response = await CallTool("4", "document_index", new Dictionary<string, object> { ["category"] = Oracles });

        response.ShouldHaveSucceeded();
        ToolPayload(response).GetProperty("documents").EnumerateArray()
            .Select(d => d.GetProperty("filename").GetString())
            .Should().Equal("moons.md");
    }

    [Fact]
    public async Task RequestWritePermission_ForALeafThatDoesNotExistYet_ShouldSucceed()
    {
        await ClearTestDocuments();

        var response = await CallTool("5", "request_write_permission", new Dictionary<string, object> { ["category"] = "Campaign.Factions" });

        response.ShouldHaveSucceeded(because: "a new category is created by storing its first document, which needs permission first");
    }

    [Fact]
    public async Task AddDocument_ToACategoryThatHasBecomeAParent_ShouldBeRefused()
    {
        await ClearTestDocuments();
        PermitWritesIn(Parent);
        await AddDocument(Oracles, "moons.md", "# Moons");

        var response = await CallTool("6", "add_document", new Dictionary<string, object>
        {
            ["category"] = Parent,
            ["filename"] = "overview.md",
            ["text"] = "# Overview",
            ["indexed"] = false
        });

        response.ShouldHaveBeenRefused().Should().Contain("parent category").And.Contain(Oracles);
        (await Db.GetDocumentIndex(Parent)).Should().BeEmpty();
    }

    [Fact]
    public async Task AddDocument_UnderACategoryThatHoldsDocuments_ShouldBeRefused()
    {
        await ClearTestDocuments();
        PermitWritesIn("Campaign.Oracles.Moons");
        await AddDocument(Oracles, "moons.md", "# Moons");

        var response = await CallTool("7", "add_document", new Dictionary<string, object>
        {
            ["category"] = "Campaign.Oracles.Moons",
            ["filename"] = "red_moon.md",
            ["text"] = "# The Red Moon",
            ["indexed"] = false
        });

        response.ShouldHaveBeenRefused().Should().Contain($"'{Oracles}' above it already does");
        (await Db.GetDocumentIndex("Campaign.Oracles.Moons")).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Campaign..Oracles")]
    [InlineData(".Campaign")]
    [InlineData("Campaign.")]
    [InlineData("Campaign. Oracles")]
    public async Task Search_WithAMalformedCategory_ShouldBeRefused(string category)
    {
        var response = await CallTool("8", "search_index", new Dictionary<string, object>
        {
            ["query"] = "moons",
            ["category"] = category
        });

        response.ShouldHaveBeenRefused().Should().Contain("not a well-formed category path");
    }

    private async Task AddDocument(string category, string filename, string text, bool indexed = false)
    {
        var response = await CallTool(Guid.NewGuid().ToString(), "add_document", new Dictionary<string, object>
        {
            ["category"] = category,
            ["filename"] = filename,
            ["text"] = text,
            ["indexed"] = indexed
        });

        response.ShouldHaveSucceeded();
    }

    private async Task<JsonElement[]> SearchResults(string query, string category)
    {
        var response = await CallTool(Guid.NewGuid().ToString(), "search_index", new Dictionary<string, object>
        {
            ["query"] = query,
            ["category"] = category,
            ["topK"] = 10
        });

        response.ShouldHaveSucceeded();
        return ToolPayload(response).GetProperty("results").EnumerateArray().ToArray();
    }
}
