using FluentAssertions;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

/// <summary>
/// The section tools against the nesting from the design discussion: a '#' holding a '##' with a
/// '###' under it, followed by a second '##'. What matters throughout is where a section ends —
/// at the next header of the same or a higher level, subsections included.
/// </summary>
public class SectionEditingTests : McpServerTestBase
{
    private const string Category = "lore";
    private const string Filename = "nested.md";

    private static readonly string Nested = Lines(
        "# B",
        "",
        "text B",
        "",
        "## C",
        "",
        "text C",
        "",
        "### D",
        "",
        "text D",
        "",
        "## E",
        "",
        "text E");

    public SectionEditingTests(TestFixture fixture) : base(fixture) => PermitWritesIn(Category);

    [Fact]
    public async Task ReplaceSection_ShouldTakeTheSectionsSubsectionsWithIt_AndLeaveItsSiblingsAlone()
    {
        await StoreNestedDocument("1");

        var replaced = await CallTool("2", "replace_document_section", Arguments(
            ("section", "C"),
            ("text", Lines("## C", "", "rewritten C"))));

        replaced.ShouldHaveSucceeded();

        (await ReadContent("3")).Should().Be(Lines(
            "# B",
            "",
            "text B",
            "",
            "## C",
            "",
            "rewritten C",
            "",
            "## E",
            "",
            "text E"),
            because: "C runs until the next header at its level or above, so D went with it and E stayed");
    }

    [Fact]
    public async Task ReplaceSection_WhenTargetingTheDeepestSection_ShouldChangeOnlyThatSection()
    {
        await StoreNestedDocument("4");

        var replaced = await CallTool("5", "replace_document_section", Arguments(
            ("section", "D"),
            ("text", Lines("### D", "", "rewritten D"))));

        replaced.ShouldHaveSucceeded();

        (await ReadContent("6")).Should().Be(Lines(
            "# B",
            "",
            "text B",
            "",
            "## C",
            "",
            "text C",
            "",
            "### D",
            "",
            "rewritten D",
            "",
            "## E",
            "",
            "text E"));
    }

    [Fact]
    public async Task ReplaceSection_ShouldRenameTheSection_WhenTheHeaderLineIsRewritten()
    {
        await StoreNestedDocument("7");

        var replaced = await CallTool("8", "replace_document_section", Arguments(
            ("section", "E"),
            ("text", Lines("## F", "", "text F"))));

        replaced.ShouldHaveSucceeded();
        (await ReadContent("9")).Should().EndWith(Lines("## F", "", "text F"));
    }

    [Fact]
    public async Task ReplaceSection_WithoutTheSectionsOwnHeader_ShouldBeRefused()
    {
        await StoreNestedDocument("10");

        var replaced = await CallTool("11", "replace_document_section", Arguments(
            ("section", "C"),
            ("text", "just the prose, no header")));

        replaced.ShouldHaveBeenRefused().Should().Contain("## C",
            because: "the error has to show the header line the replacement was missing");

        (await ReadContent("12")).Should().Be(Nested, because: "a refused edit must not have written anything");
    }

    [Fact]
    public async Task ReplaceSection_WithTheHeaderAtTheWrongLevel_ShouldBeRefused()
    {
        await StoreNestedDocument("13");

        var replaced = await CallTool("14", "replace_document_section", Arguments(
            ("section", "D"),
            ("text", Lines("## D", "", "promoted without asking"))));

        replaced.ShouldHaveBeenRefused().Should().Contain("### D");
    }

    [Fact]
    public async Task ReplaceSection_WithAShallowerHeaderInsideTheText_ShouldBeRefused()
    {
        await StoreNestedDocument("15");

        var replaced = await CallTool("16", "replace_document_section", Arguments(
            ("section", "D"),
            ("text", Lines("### D", "", "text D", "", "# A new top level", "", "which would restructure the file"))));

        replaced.ShouldHaveBeenRefused().Should().Contain("would end the section");

        (await ReadContent("17")).Should().Be(Nested);
    }

    [Fact]
    public async Task ReplaceSection_ShouldMatchTheHeaderWithoutRegardToCase()
    {
        await StoreNestedDocument("18");

        var replaced = await CallTool("19", "replace_document_section", Arguments(
            ("section", "d"),
            ("text", Lines("### D", "", "rewritten D"))));

        replaced.ShouldHaveSucceeded();
        (await ReadContent("20")).Should().Contain("rewritten D");
    }

    [Fact]
    public async Task ReplaceSection_WhenTheSectionIsMissing_ShouldReturnTheDocumentsSections()
    {
        await StoreNestedDocument("21");

        var replaced = await CallTool("22", "replace_document_section", Arguments(
            ("section", "Nowhere"),
            ("text", Lines("## Nowhere", "", "text"))));

        replaced.ShouldHaveBeenRefused().Should().Contain("B > C > D").And.Contain("B > E",
            because: "listing the sections is what lets a model correct itself without refetching the document");
    }

    [Fact]
    public async Task ReplaceSection_WhenTheNameIsAmbiguous_ShouldBeRefusedUntilThePathNarrowsIt()
    {
        await ClearTestDocuments();
        await StoreDocument("23", Lines(
            "# Customs",
            "",
            "## Ironlander",
            "",
            "### Rites",
            "",
            "Ironlander rites.",
            "",
            "## Outlander",
            "",
            "### Rites",
            "",
            "Outlander rites."));

        var ambiguous = await CallTool("24", "replace_document_section", Arguments(
            ("section", "Rites"),
            ("text", Lines("### Rites", "", "rewritten"))));

        ambiguous.ShouldHaveBeenRefused().Should().Contain("Customs > Ironlander > Rites").And.Contain("Customs > Outlander > Rites");

        var qualified = await CallTool("25", "replace_document_section", Arguments(
            ("section", "Outlander > Rites"),
            ("text", Lines("### Rites", "", "rewritten"))));

        qualified.ShouldHaveSucceeded();

        var content = await ReadContent("26");
        content.Should().Contain("Ironlander rites.", because: "only the qualified section should have changed");
        content.Should().NotContain("Outlander rites.");
    }

    [Fact]
    public async Task AppendToSection_ShouldLandAfterTheSectionsLastSubsection()
    {
        await StoreNestedDocument("27");

        var appended = await CallTool("28", "append_to_document", Arguments(
            ("section", "C"),
            ("text", Lines("### G", "", "text G"))));

        appended.ShouldHaveSucceeded();

        (await ReadContent("29")).Should().Be(Lines(
            "# B",
            "",
            "text B",
            "",
            "## C",
            "",
            "text C",
            "",
            "### D",
            "",
            "text D",
            "",
            "### G",
            "",
            "text G",
            "",
            "## E",
            "",
            "text E"),
            because: "the text goes at the end of the whole section, after D, and before the section that ends it");
    }

    [Fact]
    public async Task AppendToDocument_WithoutASection_ShouldLandAtTheEnd()
    {
        await StoreNestedDocument("30");

        var appended = await CallTool("31", "append_to_document", Arguments(
            ("text", Lines("## H", "", "text H"))));

        appended.ShouldHaveSucceeded();
        (await ReadContent("32")).Should().Be(Nested + "\n\n" + Lines("## H", "", "text H"));
    }

    [Fact]
    public async Task AppendToSection_WhenTheTextOpensWithABlankLine_ShouldNotDoubleTheSeparator()
    {
        await StoreNestedDocument("59");

        (await CallTool("60", "append_to_document", Arguments(
            ("section", "C"),
            ("text", Lines("", "### G", "", "text G", ""))))).ShouldHaveSucceeded();

        (await ReadContent("61")).Should().NotContain("\n\n\n",
            because: "blank lines the text arrives with would otherwise sit on top of the one the seam adds");
    }

    [Fact]
    public async Task AppendToSection_WithAHeaderAtTheSectionsOwnLevel_ShouldBeRefused()
    {
        await StoreNestedDocument("33");

        var appended = await CallTool("34", "append_to_document", Arguments(
            ("section", "C"),
            ("text", Lines("## Not nested", "", "this would sit outside C"))));

        appended.ShouldHaveBeenRefused().Should().Contain("would end the section");

        (await ReadContent("35")).Should().Be(Nested);
    }

    [Fact]
    public async Task DeleteSection_ShouldRemoveTheSectionAndEverythingNestedUnderIt()
    {
        await StoreNestedDocument("36");

        var deleted = await CallTool("37", "delete_document_section", Arguments(("section", "C")));

        deleted.ShouldHaveSucceeded();

        (await ReadContent("38")).Should().Be(Lines(
            "# B",
            "",
            "text B",
            "",
            "## E",
            "",
            "text E"),
            because: "D was nested under C and goes with it");
    }

    [Fact]
    public async Task DeleteSection_WhenItIsTheLastSection_ShouldLeaveTheRestIntact()
    {
        await StoreNestedDocument("39");

        (await CallTool("40", "delete_document_section", Arguments(("section", "E")))).ShouldHaveSucceeded();

        (await ReadContent("41")).Should().Be(Lines(
            "# B",
            "",
            "text B",
            "",
            "## C",
            "",
            "text C",
            "",
            "### D",
            "",
            "text D"));
    }

    [Fact]
    public async Task SectionEdit_WhenTheDocumentIsMissing_ShouldReturnError()
    {
        await ClearTestDocuments();

        var response = await CallTool("42", "replace_document_section", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "never_stored.md",
            ["section"] = "C",
            ["text"] = "## C"
        });

        response.ShouldHaveBeenRefused().Should().Contain("No document named");
    }

    [Fact]
    public async Task SectionEdit_WithoutTheSummaryArgument_ShouldKeepTheStoredSummary()
    {
        await ClearTestDocuments();
        await StoreDocument("43", Nested, summary: "The summary it was stored with");

        (await CallTool("44", "replace_document_section", Arguments(
            ("section", "E"),
            ("text", Lines("## E", "", "rewritten E"))))).ShouldHaveSucceeded();

        (await Db.GetDocument(Category, Filename))!.Summary.Should().Be("The summary it was stored with",
            because: "an edit to one section says nothing about the document's summary");
    }

    [Fact]
    public async Task SectionEdit_WithASummary_ShouldReplaceIt_AndWithAnEmptyOneShouldClearIt()
    {
        await ClearTestDocuments();
        await StoreDocument("45", Nested, summary: "The original summary");

        (await CallTool("46", "append_to_document", Arguments(
            ("text", "A closing line."),
            ("summary", "A newer summary")))).ShouldHaveSucceeded();

        (await Db.GetDocument(Category, Filename))!.Summary.Should().Be("A newer summary");

        (await CallTool("47", "append_to_document", Arguments(
            ("text", "Another closing line."),
            ("summary", "")))).ShouldHaveSucceeded();

        (await Db.GetDocument(Category, Filename))!.Summary.Should().BeNull(
            because: "an empty summary is how a write asks for the stored one to go");
    }

    [Fact]
    public async Task UpdateDocument_WithoutTheSummaryArgument_ShouldKeepTheStoredSummary()
    {
        await ClearTestDocuments();
        await StoreDocument("48", Nested, summary: "The summary it was stored with");

        (await CallTool("49", "update_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = Filename,
            ["text"] = Lines("# B", "", "an entirely new body")
        })).ShouldHaveSucceeded();

        var document = await Db.GetDocument(Category, Filename);
        document!.Content.Should().Be(Lines("# B", "", "an entirely new body"));
        document.Summary.Should().Be("The summary it was stored with");
    }

    [Fact]
    public async Task SectionEdit_OnAnIndexedDocument_ShouldRebuildTheIndexFromWhatItLeavesBehind()
    {
        await ClearTestDocuments();
        await StoreDocument("50", Nested, indexed: true);

        (await CallTool("51", "replace_document_section", Arguments(
            ("section", "E"),
            ("text", Lines("## E", "", "A luminous derelict hangs above the shattered moon."))))).ShouldHaveSucceeded();

        (await Db.GetDocument(Category, Filename))!.Indexed.Should().BeTrue();

        var results = await CallTool("52", "search_index", new Dictionary<string, object>
        {
            ["query"] = "a derelict above a shattered moon",
            ["category"] = Category
        });

        ToolPayload(results).GetProperty("results").EnumerateArray()
            .Should().NotBeEmpty(because: "the new text has to be searchable without a separate index_document call");
    }

    [Fact]
    public async Task SectionEdit_OnAnUnindexedDocument_ShouldLeaveItUnindexed()
    {
        await ClearTestDocuments();
        await StoreDocument("53", Nested, indexed: false);

        (await CallTool("54", "append_to_document", Arguments(("text", "A closing line.")))).ShouldHaveSucceeded();

        (await Db.GetDocument(Category, Filename))!.Indexed.Should().BeFalse(
            because: "editing a document is not a decision to start indexing it");
    }

    [Fact]
    public async Task IndexDocument_AndDeindexDocument_ShouldTurnSearchabilityOnAndOffWithoutTouchingTheContent()
    {
        await ClearTestDocuments();
        await StoreDocument("55", Nested, indexed: false);

        (await CallTool("56", "index_document", Arguments())).ShouldHaveSucceeded();

        var indexed = await Db.GetDocument(Category, Filename);
        indexed!.Indexed.Should().BeTrue();
        indexed.Content.Should().Be(Nested);

        (await CallTool("57", "deindex_document", Arguments())).ShouldHaveSucceeded();

        var deindexed = await Db.GetDocument(Category, Filename);
        deindexed!.Indexed.Should().BeFalse();
        deindexed.Content.Should().Be(Nested, because: "de-indexing drops the embeddings, not the document");
    }

    [Fact]
    public async Task IndexDocument_WhenTheDocumentIsMissing_ShouldReturnError()
    {
        await ClearTestDocuments();

        var response = await CallTool("58", "index_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = "never_stored.md"
        });

        response.ShouldHaveBeenRefused().Should().Contain("No document named");
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    private static Dictionary<string, object> Arguments(params (string Key, string Value)[] arguments)
    {
        var result = new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = Filename
        };

        foreach (var (key, value) in arguments)
            result[key] = value;

        return result;
    }

    private async Task StoreNestedDocument(string id)
    {
        await ClearTestDocuments();
        await StoreDocument(id, Nested);
    }

    private async Task StoreDocument(string id, string text, string? summary = null, bool indexed = false)
    {
        var arguments = new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = Filename,
            ["text"] = text,
            ["indexed"] = indexed
        };

        if (summary != null) arguments["summary"] = summary;

        (await CallTool(id, "add_document", arguments)).ShouldHaveSucceeded();
    }

    private async Task<string> ReadContent(string id)
    {
        var fetched = await CallTool(id, "get_document", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["filename"] = Filename
        });

        fetched.ShouldHaveSucceeded();
        return ToolPayload(fetched).GetProperty("document").GetProperty("content").GetString()!;
    }
}
