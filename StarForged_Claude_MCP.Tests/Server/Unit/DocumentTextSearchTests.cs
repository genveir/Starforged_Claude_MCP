using FluentAssertions;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class DocumentTextSearchTests
{
    [Fact]
    public void Search_ShouldIgnoreCase()
    {
        var result = DocumentTextSearch.Search([Doc("crew.md", "Mara BLUEJAY signed on at Bleakhold.")], "bluejay", wholeWord: false);

        result.TotalMatches.Should().Be(1);
        result.Documents.Single().Snippets.Single().Text.Should().Be("Mara BLUEJAY signed on at Bleakhold.");
    }

    [Fact]
    public void Search_WithoutWholeWord_ShouldMatchInsideLongerWords()
    {
        var result = DocumentTextSearch.Search([Doc("crew.md", "Mara Bluejay and Jay Okoro.")], "jay", wholeWord: false);

        result.TotalMatches.Should().Be(2);
    }

    [Fact]
    public void Search_WithWholeWord_ShouldSkipLongerWords_ButMatchPossessives()
    {
        var result = DocumentTextSearch.Search(
            [Doc("crew.md", "Mara Bluejay met Jay. Jay's ship is the Kestrel.")], "jay", wholeWord: true);

        result.TotalMatches.Should().Be(2, because: "'Bluejay' is not the word 'Jay', but \"Jay's\" is");
    }

    [Theory]
    [InlineData("50%")]
    [InlineData("[GM]")]
    [InlineData("a_b")]
    [InlineData(".*")]
    public void Search_ShouldTreatPatternCharactersLiterally(string text)
    {
        var documents = new[]
        {
            Doc("literal.md", $"The note says {text} here."),
            Doc("other.md", "The note says 500 here, GM, ab, anything.")
        };

        var result = DocumentTextSearch.Search(documents, text, wholeWord: false);

        result.Documents.Select(d => d.Filename).Should().Equal("literal.md");
    }

    [Fact]
    public void Search_WithWholeWord_ShouldFindAPhraseThatStartsAndEndsInPunctuation()
    {
        var result = DocumentTextSearch.Search([Doc("notes.md", "Ask the [GM] first.")], "[GM]", wholeWord: true);

        result.TotalMatches.Should().Be(1);
    }

    [Fact]
    public void Search_ShouldMatchAPhraseWrappedOntoTheNextLine()
    {
        var result = DocumentTextSearch.Search(
            [Doc("lore.md", "They sheltered behind the Iron\r\nVeil for a week.")], "the iron veil", wholeWord: false);

        result.TotalMatches.Should().Be(1);
        result.Documents.Single().Snippets.Single().Text.Should().Be("They sheltered behind the Iron Veil for a week.");
    }

    [Fact]
    public void Search_ShouldLabelSnippetsWithTheSectionPath_AndNullBeforeTheFirstHeader()
    {
        var content = Lines(
            "Bluejay, before any header.",
            "# Crew",
            "## Mara Bluejay",
            "First officer.",
            "```",
            "# not a header, Bluejay",
            "```",
            "## Tomas",
            "Owes Bluejay a debt.");

        var snippets = DocumentTextSearch.Search([Doc("crew.md", content)], "Bluejay", wholeWord: false)
            .Documents.Single().Snippets;

        snippets.Select(s => s.Section).Should().Equal(
            null,
            "Crew > Mara Bluejay",
            "Crew > Mara Bluejay",
            "Crew > Tomas");
    }

    [Fact]
    public void Search_ShouldCutLongContextAtWordBoundaries_AndMarkTheCuts()
    {
        var before = string.Join(" ", Enumerable.Repeat("before", 60));
        var after = string.Join(" ", Enumerable.Repeat("after", 60));

        var snippet = DocumentTextSearch.Search([Doc("long.md", $"{before} Bluejay {after}")], "Bluejay", wholeWord: false)
            .Documents.Single().Snippets.Single().Text;

        snippet.Should().StartWith("…before ").And.EndWith(" after…").And.Contain(" Bluejay ");
        snippet.Length.Should().BeLessThan(320);
    }

    [Fact]
    public void Search_ShouldNotRunASnippetIntoTheNextLine()
    {
        var snippet = DocumentTextSearch.Search(
            [Doc("crew.md", Lines("## Mara Bluejay", "First officer of the Kestrel."))], "Bluejay", wholeWord: false)
            .Documents.Single().Snippets.Single().Text;

        snippet.Should().Be("## Mara Bluejay");
    }

    [Fact]
    public void Search_ShouldMergeNearbyMatchesIntoOneSnippet_ButCountEachOne()
    {
        var document = DocumentTextSearch.Search(
            [Doc("crew.md", "Bluejay spoke to Bluejay's brother.")], "Bluejay", wholeWord: false)
            .Documents.Single();

        document.MatchCount.Should().Be(2);
        document.Snippets.Should().ContainSingle().Which.Text.Should().Be("Bluejay spoke to Bluejay's brother.");
    }

    [Fact]
    public void Search_ShouldCapSnippetsPerDocument_ButReportTheFullCount()
    {
        var content = Lines(Enumerable.Range(1, 8).Select(i => $"Line {i} mentions Bluejay.").ToArray());

        var document = DocumentTextSearch.Search([Doc("crew.md", content)], "Bluejay", wholeWord: false)
            .Documents.Single();

        document.MatchCount.Should().Be(8);
        document.Snippets.Should().HaveCount(DocumentTextSearch.MaxSnippetsPerDocument);
    }

    [Fact]
    public void Search_ShouldOrderByMatchCount_ThenFilename()
    {
        var documents = new[]
        {
            Doc("b.md", "Bluejay."),
            Doc("a.md", "Bluejay."),
            Doc("c.md", "Bluejay and Bluejay.")
        };

        var result = DocumentTextSearch.Search(documents, "Bluejay", wholeWord: false);

        result.Documents.Select(d => d.Filename).Should().Equal("c.md", "a.md", "b.md");
    }

    [Fact]
    public void Search_ShouldCapDocuments_AndSayItTruncated()
    {
        var documents = Enumerable.Range(1, DocumentTextSearch.MaxDocuments + 3)
            .Select(i => Doc($"session_{i:D2}.md", "Bluejay."));

        var result = DocumentTextSearch.Search(documents, "Bluejay", wholeWord: false);

        result.Documents.Should().HaveCount(DocumentTextSearch.MaxDocuments);
        result.Truncated.Should().BeTrue();
        result.TotalMatches.Should().Be(DocumentTextSearch.MaxDocuments + 3,
            because: "the total counts every match, not just those in the files listed");
    }

    [Fact]
    public void Search_ShouldDropDocumentsWithoutAMatch_AndCarryTheSummaryOfThoseWithOne()
    {
        var documents = new[]
        {
            Doc("crew.md", "Mara Bluejay.", summary: "The crew"),
            Doc("ships.md", "The Kestrel.")
        };

        var result = DocumentTextSearch.Search(documents, "Bluejay", wholeWord: false);

        result.Truncated.Should().BeFalse();
        result.Documents.Should().ContainSingle().Which.Summary.Should().Be("The crew");
    }

    private static Document Doc(string filename, string content, string? summary = null) =>
        new() { Category = "lore", Filename = filename, Content = content, Summary = summary };

    private static string Lines(params string[] lines) => string.Join("\n", lines);
}
