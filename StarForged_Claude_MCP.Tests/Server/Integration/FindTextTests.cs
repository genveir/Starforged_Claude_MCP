using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class FindTextTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Category = "lore";

    [Fact]
    public async Task FindText_ShouldFindUnindexedDocuments_WithinTheCategoryOnly()
    {
        await ClearTestDocuments();
        await Db.StoreDocument(Category, "crew.md", "# Crew\n\n## Mara Bluejay\n\nFirst officer.", summary: "The crew");
        await Db.StoreDocument(Category, "ships.md", "# Ships\n\nThe Kestrel.", summary: null);
        await Db.StoreDocument("other", "crew.md", "# Crew\n\nA different Bluejay.", summary: null);

        var response = await CallTool("1", "find_text", Arguments(("text", "bluejay")));

        response.ShouldHaveSucceeded();
        var payload = ToolPayload(response);
        payload.GetProperty("totalMatches").GetInt32().Should().Be(1);
        payload.GetProperty("truncated").GetBoolean().Should().BeFalse();

        var document = payload.GetProperty("documents").EnumerateArray().Should().ContainSingle().Subject;
        document.GetProperty("filename").GetString().Should().Be("crew.md");
        document.GetProperty("summary").GetString().Should().Be("The crew");

        var snippet = document.GetProperty("snippets").EnumerateArray().Should().ContainSingle().Subject;
        snippet.GetProperty("section").GetString().Should().Be("Crew > Mara Bluejay");
        snippet.GetProperty("text").GetString().Should().Be("## Mara Bluejay");
    }

    [Theory]
    [InlineData("50%")]
    [InlineData("a_b")]
    [InlineData("[GM]")]
    [InlineData(@"C:\logs")]
    public async Task FindText_ShouldTreatLikeWildcardsLiterally(string text)
    {
        await ClearTestDocuments();
        await Db.StoreDocument(Category, "literal.md", $"The note says {text} here.", summary: null);
        await Db.StoreDocument(Category, "other.md", "The note says 500, axb, G, M, C:logs here.", summary: null);

        var response = await CallTool("2", "find_text", Arguments(("text", text)));

        response.ShouldHaveSucceeded();
        Filenames(response).Should().Equal("literal.md");
    }

    [Fact]
    public async Task FindText_ShouldMatchAPhraseAcrossALineBreak()
    {
        await ClearTestDocuments();
        await Db.StoreDocument(Category, "lore.md", "They sheltered behind the Iron\r\nVeil for a week.", summary: null);

        var response = await CallTool("3", "find_text", Arguments(("text", "the iron veil")));

        response.ShouldHaveSucceeded();
        Filenames(response).Should().Equal("lore.md");
    }

    [Fact]
    public async Task FindText_WithWholeWord_ShouldSkipDocumentsWhereTheTextIsOnlyPartOfAWord()
    {
        await ClearTestDocuments();
        await Db.StoreDocument(Category, "bluejay.md", "Mara Bluejay.", summary: null);
        await Db.StoreDocument(Category, "jay.md", "Jay's ship.", summary: null);

        var response = await CallTool("4", "find_text", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["text"] = "jay",
            ["wholeWord"] = true
        });

        response.ShouldHaveSucceeded();
        Filenames(response).Should().Equal("jay.md");
    }

    [Fact]
    public async Task FindText_WithAFilename_ShouldSearchOnlyThatDocument()
    {
        await ClearTestDocuments();
        await Db.StoreDocument(Category, "crew.md", "Mara Bluejay.", summary: null);
        await Db.StoreDocument(Category, "session_4.md", "Bluejay and Bluejay again.", summary: null);

        var response = await CallTool("5", "find_text", Arguments(("text", "Bluejay"), ("filename", "crew.md")));

        response.ShouldHaveSucceeded();
        Filenames(response).Should().Equal("crew.md");
    }

    [Fact]
    public async Task FindText_WithAFilenameThatDoesNotExist_ShouldBeRefused()
    {
        await ClearTestDocuments();

        var response = await CallTool("6", "find_text", Arguments(("text", "Bluejay"), ("filename", "never_stored.md")));

        response.ShouldHaveBeenRefused().Should().Contain("No document named",
            because: "an empty result would read as the document not mentioning the text");
    }

    [Fact]
    public async Task FindText_WhenNothingMatches_ShouldReturnAnEmptyList()
    {
        await ClearTestDocuments();
        await Db.StoreDocument(Category, "ships.md", "The Kestrel.", summary: null);

        var response = await CallTool("7", "find_text", Arguments(("text", "Bluejay")));

        response.ShouldHaveSucceeded();
        Filenames(response).Should().BeEmpty();
        ToolPayload(response).GetProperty("totalMatches").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("x", "at least 2 characters")]
    [InlineData("   ", "cannot be empty")]
    public async Task FindText_WithTooShortAText_ShouldBeRefused(string text, string expected)
    {
        var response = await CallTool("8", "find_text", Arguments(("text", text)));

        response.ShouldHaveBeenRefused().Should().Contain(expected);
    }

    [Fact]
    public async Task FindText_WithWholeWordAsText_ShouldBeRefused()
    {
        var response = await CallTool("9", "find_text", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["text"] = "Bluejay",
            ["wholeWord"] = "yes"
        });

        response.ShouldHaveBeenRefused().Should().Contain("WholeWord");
    }

    private List<string> Filenames(JsonRpcResponse response) =>
        ToolPayload(response).GetProperty("documents").EnumerateArray()
            .Select(document => document.GetProperty("filename").GetString()!)
            .ToList();

    private static Dictionary<string, object> Arguments(params (string Key, string Value)[] arguments)
    {
        var result = new Dictionary<string, object> { ["category"] = Category };

        foreach (var (key, value) in arguments)
            result[key] = value;

        return result;
    }
}
