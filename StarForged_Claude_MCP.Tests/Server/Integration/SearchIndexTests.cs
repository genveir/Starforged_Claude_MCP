using FluentAssertions;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class SearchIndexTests : McpServerTestBase
{
    private const string Category = "lore";

    public SearchIndexTests(TestFixture fixture) : base(fixture) =>
        PermitWritesIn(Category, "session_log");

    [Fact]
    public async Task Search_WithValidQuery_ShouldReturnResults()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("wizard.md", "The wizard cast a powerful fireball spell.");
        await AddIndexedDocument("rogue.md", "The rogue snuck past the guards silently.");

        var summaries = await SearchSummaries("magic spells", topK: 2);

        summaries.Should().Contain("The wizard cast a powerful fireball spell.");
        summaries.Should().Contain("The rogue snuck past the guards silently.");
    }

    [Fact]
    public async Task Search_SemanticRelevance_ShouldReturnTopTwoRelatedEntries()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("blacksmith.md", "The blacksmith hammered the glowing iron on the anvil.");
        await AddIndexedDocument("baking.md", "She baked a sourdough loaf with rosemary and sea salt.");
        await AddIndexedDocument("spacecraft.md", "The spacecraft entered orbit around the red planet.");
        await AddIndexedDocument("fog.md", "A dense fog rolled over the mountain peaks at dawn.");
        await AddIndexedDocument("chess.md", "The chess grandmaster sacrificed his queen to secure the endgame.");

        await AddIndexedDocument("reef.md", "The coral reef teems with colorful tropical fish and sea anemones.");
        await AddIndexedDocument("dolphins.md", "Dolphins are highly intelligent marine mammals that live in the ocean.");

        await AddIndexedDocument("lightning.md", "Lightning struck the old oak tree at the edge of the field.");
        await AddIndexedDocument("orchestra.md", "The orchestra performed Beethoven's Fifth Symphony to a standing ovation.");
        await AddIndexedDocument("debugging.md", "He debugged the memory leak by profiling heap allocations.");
        await AddIndexedDocument("tax.md", "The tax reform bill passed through the senate with a narrow majority.");
        await AddIndexedDocument("aqueducts.md", "Ancient Roman aqueducts supplied fresh water to cities across the empire.");

        var summaries = await SearchSummaries("ocean wildlife and sea creatures", topK: 2);

        summaries.Should().HaveCount(2);
        summaries.Should().Contain("The coral reef teems with colorful tropical fish and sea anemones.");
        summaries.Should().Contain("Dolphins are highly intelligent marine mammals that live in the ocean.");
    }

    [Fact]
    public async Task Search_ShouldOnlyConsiderTheRequestedCategory()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("dragon.md", "The ancient dragon guarded its hoard deep beneath the mountain.", category: "session_log");

        var otherCategory = await SearchResults("sleeping dragon", topK: 5, category: "lore");
        var ownCategory = await SearchResults("sleeping dragon", topK: 5, category: "session_log");

        otherCategory.Should().BeEmpty(because: "the only matching document lives in a different category");
        ownCategory.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_ShouldNotFindDocumentsStoredWithoutIndexing()
    {
        await ClearTestDocuments();

        await CallTool("30", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "unindexed.md",
            ["text"] = "The ancient dragon guarded its hoard deep beneath the mountain.",
            ["indexed"] = false
        });

        var results = await SearchResults("sleeping dragon", topK: 5);

        results.Should().BeEmpty(because: "a document stored with indexed=false is never chunked or embedded");
    }

    [Fact]
    public async Task Search_ShouldReturnTheFilenameOfTheDocumentAChunkCameFrom()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("dragon_lore.md", "The ancient dragon guarded its hoard deep beneath the mountain.");

        var results = await SearchResults("dragon hoard", topK: 1);

        results.Should().ContainSingle();
        results[0].GetProperty("filename").GetString().Should().Be("dragon_lore.md");
    }

    [Fact]
    public async Task Search_AfterUpdate_ShouldFindTheNewContentAndNotTheOld()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("changeable.md", "The coral reef teems with colorful tropical fish.");

        await CallTool("31", "update_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "changeable.md",
            ["text"] = "The blacksmith hammered the glowing iron on the anvil.",
            ["indexed"] = true
        });

        var summaries = await SearchSummaries("coral reef tropical fish", topK: 5);

        summaries.Should().NotContain("The coral reef teems with colorful tropical fish.",
            because: "an update replaces the content outright, so the old chunks are discarded");
        summaries.Should().Contain("The blacksmith hammered the glowing iron on the anvil.");
    }

    [Fact]
    public async Task Search_AfterIndexingIsTurnedOff_ShouldFindNothing()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("withdrawn.md", "The coral reef teems with colorful tropical fish.");

        await CallTool("32", "update_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "withdrawn.md",
            ["text"] = "The coral reef teems with colorful tropical fish.",
            ["indexed"] = false
        });

        var results = await SearchResults("coral reef tropical fish", topK: 5);

        results.Should().BeEmpty(because: "updating with indexed=false removes what was indexed for the document");
    }

    [Fact]
    public async Task Search_AfterTheDocumentIsArchived_ShouldFindNothing()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("temporary.md", "The coral reef teems with colorful tropical fish.");

        await CallTool("33", "archive_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "temporary.md"
        });

        var results = await SearchResults("coral reef tropical fish", topK: 5);

        results.Should().BeEmpty(because: "the foreign key cascades, so a deleted document takes its chunks with it");
    }

    [Fact]
    public async Task SearchIndex_ShortFirstSentence_ShouldReturnFirstTwoSentences()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("short_first.md",
            "The dragon slept. It had guarded its vast golden hoard for centuries. No one had ever dared disturb it.");

        var summary = (await SearchSummaries("sleeping dragon", topK: 1)).Single();

        summary.Should().Be("The dragon slept. It had guarded its vast golden hoard for centuries.");
        summary.Should().NotContain("No one had ever dared disturb it.");
    }

    [Fact]
    public async Task SearchIndex_LongFirstSentence_ShouldReturnFirstSentenceOnly()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("long_first.md",
            "The ancient dragon had guarded its vast golden hoard for centuries. No one had ever dared disturb it.");

        var summary = (await SearchSummaries("dragon guarding hoard", topK: 1)).Single();

        summary.Should().Be("The ancient dragon had guarded its vast golden hoard for centuries.");
        summary.Should().NotContain("No one had ever dared disturb it.");
    }

    [Fact]
    public async Task SearchIndex_BreadcrumbText_ShouldPreservePrefixInSummary()
    {
        await ClearTestDocuments();

        await AddIndexedDocument("breadcrumb.md",
            "Dragon's Lair: The ancient dragon had guarded its vast golden hoard for centuries. No one had ever dared disturb it.");

        var summary = (await SearchSummaries("dragon lair hoard", topK: 1)).Single();

        summary.Should().Be("Dragon's Lair: The ancient dragon had guarded its vast golden hoard for centuries.");
        summary.Should().NotContain("No one had ever dared disturb it.");
    }

    [Fact]
    public async Task Search_WithoutCategory_ShouldReturnError()
    {
        var response = await CallTool("34", "search_index", new Dictionary<string, object>
        {
            ["query"] = "anything at all"
        });

        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32602);
        response.Error.Message.Should().Contain("Category cannot be empty");
    }

    private async Task AddIndexedDocument(string filename, string text, string category = Category)
    {
        var response = await CallTool(Guid.NewGuid().ToString(), "add_document", new Dictionary<string, object>
        {
            ["category"] = category,
            ["filename"] = filename,
            ["text"] = text,
            ["indexed"] = true
        });

        response.Error.Should().BeNull();
    }

    private async Task<JsonElement[]> SearchResults(string query, int topK, string category = Category)
    {
        var response = await CallTool(Guid.NewGuid().ToString(), "search_index", new Dictionary<string, object>
        {
            ["query"] = query,
            ["category"] = category,
            ["topK"] = topK
        });

        response.Error.Should().BeNull();
        return ToolPayload(response).GetProperty("results").EnumerateArray().ToArray();
    }

    private async Task<string[]> SearchSummaries(string query, int topK, string category = Category) =>
        (await SearchResults(query, topK, category))
            .Select(r => r.GetProperty("summary").GetString()!)
            .ToArray();
}
