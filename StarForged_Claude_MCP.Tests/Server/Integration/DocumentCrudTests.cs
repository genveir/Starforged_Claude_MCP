using FluentAssertions;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class DocumentCrudTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Category = "lore";

    [Fact]
    public async Task AddDocument_ShouldRoundTripThroughGetDocument()
    {
        await ClearTestDocuments();

        var added = await CallTool("1", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "tavern.md",
            ["text"] = "The hero entered the tavern at midnight.",
            ["summary"] = "A midnight arrival",
            ["indexed"] = false
        });

        added.Error.Should().BeNull();

        var fetched = await CallTool("2", "get_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "tavern.md"
        });

        fetched.Error.Should().BeNull();

        var document = ToolPayload(fetched).GetProperty("document");
        document.GetProperty("filename").GetString().Should().Be("tavern.md");
        document.GetProperty("category").GetString().Should().Be(Category);
        document.GetProperty("content").GetString().Should().Be("The hero entered the tavern at midnight.");
        document.GetProperty("summary").GetString().Should().Be("A midnight arrival");
        document.GetProperty("indexed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task AddDocument_WhenFilenameAlreadyUsedInCategory_ShouldReturnError()
    {
        await ClearTestDocuments();

        var arguments = () => new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "duplicate.md",
            ["text"] = "The first one wins.",
            ["indexed"] = false
        };

        (await CallTool("3", "add_document", arguments())).Error.Should().BeNull();

        var second = await CallTool("4", "add_document", arguments());

        second.Error.Should().NotBeNull();
        second.Error.Code.Should().Be(-32602);
        second.Error.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task AddDocument_WithSameFilenameInAnotherCategory_ShouldSucceed()
    {
        await ClearTestDocuments();

        var makeArguments = (string category) => new Dictionary<string, object>
        {
            ["category"] = category,
            ["filename"] = "shared_name.md",
            ["text"] = $"Content for {category}.",
            ["indexed"] = false
        };

        (await CallTool("5", "add_document", makeArguments("lore"))).Error.Should().BeNull();
        (await CallTool("6", "add_document", makeArguments("session_log"))).Error.Should().BeNull();

        var fetched = await CallTool("7", "get_document", new Dictionary<string, object>
        {
            ["category"] = "session_log",
            ["filename"] = "shared_name.md"
        });

        ToolPayload(fetched).GetProperty("document").GetProperty("content").GetString()
            .Should().Be("Content for session_log.", because: "a filename is only unique within its own category");
    }

    [Fact]
    public async Task UpdateDocument_ShouldReplaceContentWholesale()
    {
        await ClearTestDocuments();

        await CallTool("8", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "revised.md",
            ["text"] = "The original text.",
            ["summary"] = "Original summary",
            ["indexed"] = false
        });

        var updated = await CallTool("9", "update_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "revised.md",
            ["text"] = "The replacement text.",
            ["summary"] = "New summary",
            ["indexed"] = false
        });

        updated.Error.Should().BeNull();

        var fetched = await CallTool("10", "get_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "revised.md"
        });

        var document = ToolPayload(fetched).GetProperty("document");
        document.GetProperty("content").GetString().Should().Be("The replacement text.");
        document.GetProperty("summary").GetString().Should().Be("New summary");
    }

    [Fact]
    public async Task UpdateDocument_WhenMissing_ShouldReturnError()
    {
        await ClearTestDocuments();

        var response = await CallTool("11", "update_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "never_stored.md",
            ["text"] = "Nothing to replace.",
            ["indexed"] = false
        });

        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32602);
        response.Error.Message.Should().Contain("No document named");
    }

    [Fact]
    public async Task DeleteDocument_ShouldRemoveIt()
    {
        await ClearTestDocuments();

        await CallTool("12", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "doomed.md",
            ["text"] = "Not long for this world.",
            ["indexed"] = false
        });

        (await CallTool("13", "delete_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "doomed.md"
        })).Error.Should().BeNull();

        var fetched = await CallTool("14", "get_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "doomed.md"
        });

        fetched.Error.Should().NotBeNull();
        fetched.Error.Message.Should().Contain("No document named");
    }

    [Fact]
    public async Task DeleteDocument_WhenMissing_ShouldReturnError()
    {
        await ClearTestDocuments();

        var response = await CallTool("15", "delete_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "never_stored.md"
        });

        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32602);
    }

    [Fact]
    public async Task DocumentIndex_ShouldListTheCategorysDocumentsWithoutContent()
    {
        await ClearTestDocuments();

        await CallTool("16", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "beta.md",
            ["text"] = "Second alphabetically.",
            ["summary"] = "Beta summary",
            ["indexed"] = false
        });
        await CallTool("17", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "alpha.md",
            ["text"] = "First alphabetically.",
            ["indexed"] = true
        });
        await CallTool("18", "add_document", new Dictionary<string, object>
        {
            ["category"] = "other_category",
            ["filename"] = "elsewhere.md",
            ["text"] = "Belongs to another category.",
            ["indexed"] = false
        });

        var response = await CallTool("19", "document_index", new Dictionary<string, object>
        {
            ["category"] = Category
        });

        response.Error.Should().BeNull();

        var documents = ToolPayload(response).GetProperty("documents").EnumerateArray().ToArray();

        documents.Should().HaveCount(2, because: "only documents in the requested category are listed");
        documents[0].GetProperty("filename").GetString().Should().Be("alpha.md");
        documents[0].GetProperty("indexed").GetBoolean().Should().BeTrue();
        documents[0].TryGetProperty("summary", out _).Should().BeFalse(because: "this one was stored without a summary");
        documents[1].GetProperty("filename").GetString().Should().Be("beta.md");
        documents[1].GetProperty("summary").GetString().Should().Be("Beta summary");
        documents[1].TryGetProperty("content", out _).Should().BeFalse(because: "the index is a listing, not the content");
    }

    [Fact]
    public async Task GetDocumentSummary_ShouldReturnTheSummaryWithoutTheContent()
    {
        await ClearTestDocuments();

        await CallTool("22", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "summarised.md",
            ["text"] = "# Section" + Environment.NewLine + Environment.NewLine + "A good deal of content that should not come back.",
            ["summary"] = "What this file is about",
            ["indexed"] = true
        });

        var response = await CallTool("23", "get_document_summary", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "summarised.md"
        });

        response.Error.Should().BeNull();

        var payload = ToolPayload(response);
        payload.GetProperty("filename").GetString().Should().Be("summarised.md");
        payload.GetProperty("summary").GetString().Should().Be("What this file is about");
        payload.GetProperty("indexed").GetBoolean().Should().BeTrue();
        payload.TryGetProperty("content", out _).Should().BeFalse(because: "this is the summary, not the document");
    }

    [Fact]
    public async Task GetDocumentSummary_WhenStoredWithoutOne_ShouldReturnAnExplicitNull()
    {
        await ClearTestDocuments();

        // Console uploads never set a summary, so this is the common case for anything
        // that did not come from a model.
        await Db.StoreDocument(Category, "from_the_console.md", "Some content.", summary: null, indexed: false);

        var response = await CallTool("24", "get_document_summary", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "from_the_console.md"
        });

        response.Error.Should().BeNull();

        var payload = ToolPayload(response);
        payload.TryGetProperty("summary", out var summary).Should().BeTrue(
            because: "an absent field would read as though documents had no summaries at all");
        summary.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetDocumentSummary_WhenMissing_ShouldReturnError()
    {
        await ClearTestDocuments();

        var response = await CallTool("25", "get_document_summary", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "never_stored.md"
        });

        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32602);
        response.Error.Message.Should().Contain("No document named");
    }

    [Fact]
    public async Task AddDocument_WithoutCategory_ShouldReturnError()
    {
        var response = await CallTool("20", "add_document", new Dictionary<string, object>
        {
            ["filename"] = "orphan.md",
            ["text"] = "No category given.",
            ["indexed"] = false
        });

        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32602);
        response.Error.Message.Should().Contain("Category cannot be empty");
    }

    [Fact]
    public async Task AddDocument_WithoutIndexedFlag_ShouldReturnError()
    {
        var response = await CallTool("21", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "undecided.md",
            ["text"] = "No indexing decision given."
        });

        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32602);
        response.Error.Message.Should().Contain("Indexed is required");
    }
}
