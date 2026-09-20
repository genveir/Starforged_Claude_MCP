using FluentAssertions;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

/// <summary>
/// Malformed arguments used to reach the caller as an unhandled conversion failure, which says only
/// that something went wrong. Stringified numbers and booleans are still rejected — they are just
/// rejected in terms the caller can act on.
/// </summary>
public class ArgumentRefusalTests : McpServerTestBase
{
    private const string Category = "lore";

    public ArgumentRefusalTests(TestFixture fixture) : base(fixture) => PermitWritesIn(Category);

    [Fact]
    public async Task RetrieveSearchResults_WithoutIds_ShouldSayWhichArgumentIsMissing()
    {
        var response = await CallTool("1", "retrieve_search_results", new Dictionary<string, object>());

        response.ShouldHaveBeenRefused().Should().Contain("Ids is required");
    }

    [Fact]
    public async Task RetrieveSearchResults_WithIdsAsText_ShouldSayWhatWasExpected()
    {
        var response = await CallTool("2", "retrieve_search_results", new Dictionary<string, object>
        {
            ["ids"] = new object[] { "7" }
        });

        var refusal = response.ShouldHaveBeenRefused();
        refusal.Should().Contain("Ids entries").And.Contain("whole number");
        refusal.Should().Contain("\"7\"", because: "naming what was sent is what makes the message actionable");
    }

    [Fact]
    public async Task RetrieveSearchResults_WithIdsThatAreNotAList_ShouldSayAListWasExpected()
    {
        var response = await CallTool("3", "retrieve_search_results", new Dictionary<string, object>
        {
            ["ids"] = 7
        });

        response.ShouldHaveBeenRefused().Should().Contain("list of whole numbers");
    }

    [Fact]
    public async Task GetCanonicalBeats_WithoutSessionNumber_ShouldSayWhichArgumentIsMissing()
    {
        var response = await CallTool("4", "get_canonical_beats", new Dictionary<string, object>
        {
            ["category"] = Category
        });

        response.ShouldHaveBeenRefused().Should().Contain("SessionNumber is required");
    }

    [Fact]
    public async Task GetCanonicalBeats_WithSessionNumberAsText_ShouldBeRefusedRatherThanParsed()
    {
        var response = await CallTool("5", "get_canonical_beats", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["sessionNumber"] = "5"
        });

        response.ShouldHaveBeenRefused().Should().Contain("SessionNumber").And.Contain("whole number");
    }

    [Fact]
    public async Task SearchIndex_WithTopKAsText_ShouldBeRefusedRatherThanParsed()
    {
        var response = await CallTool("6", "search_index", new Dictionary<string, object>
        {
            ["query"] = "a derelict in the Forge",
            ["category"] = Category,
            ["topK"] = "3"
        });

        response.ShouldHaveBeenRefused().Should().Contain("TopK").And.Contain("in quotes");
    }

    [Fact]
    public async Task AddDocument_WithIndexedAsText_ShouldSayItWantsAnUnquotedBoolean()
    {
        var response = await CallTool("7", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "quoted_flag.md",
            ["text"] = "# Quoted",
            ["indexed"] = "false"
        });

        response.ShouldHaveBeenRefused().Should().Contain("Indexed").And.Contain("true or false");
    }

    [Fact]
    public async Task AddDocument_WithIndexedAsANumber_ShouldSayItWantsABoolean()
    {
        var response = await CallTool("8", "add_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "numeric_flag.md",
            ["text"] = "# Numeric",
            ["indexed"] = 0
        });

        response.ShouldHaveBeenRefused().Should().Contain("Indexed").And.Contain("true or false");
    }

    /// <summary>
    /// Arguments that arrive over stdio are JsonElements rather than the CLR values an in-process call
    /// passes, and that is the branch the unhandled conversions were on, so these go in as real JSON.
    /// </summary>
    [Theory]
    [InlineData("search_index", """{"query":"q","category":"lore","topK":"3"}""", "TopK")]
    [InlineData("retrieve_search_results", """{"ids":["7"]}""", "Ids entries")]
    [InlineData("retrieve_search_results", """{"ids":7}""", "list of whole numbers")]
    [InlineData("retrieve_search_results", "{}", "Ids is required")]
    [InlineData("get_canonical_beats", """{"category":"lore","sessionNumber":"5"}""", "SessionNumber")]
    [InlineData("get_canonical_beats", """{"category":"lore"}""", "SessionNumber is required")]
    [InlineData("add_document", """{"category":"lore","filename":"w.md","text":"# W","indexed":"false"}""", "true or false")]
    [InlineData("search_index", """{"query":"q","category":"lore","topK":3.5}""", "TopK")]
    public async Task MalformedArgument_ArrivingAsJson_ShouldBeRefusedInTermsTheCallerCanAct(
        string toolName, string argumentsJson, string expected)
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson)!;

        var response = await CallTool("10", toolName, arguments);

        response.ShouldHaveBeenRefused().Should().Contain(expected);
    }

    [Fact]
    public async Task SearchIndex_WithoutTopK_ShouldStillRunOnTheDefault()
    {
        var response = await CallTool("9", "search_index", new Dictionary<string, object>
        {
            ["query"] = "a derelict in the Forge",
            ["category"] = Category
        });

        response.ShouldHaveSucceeded(because: "topK is optional and defaults to 3");
    }
}
