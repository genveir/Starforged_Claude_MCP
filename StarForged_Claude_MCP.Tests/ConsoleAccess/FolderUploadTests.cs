using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.ConsoleAccess.Upload;
using StarForged_Claude_MCP.Embeddings.Services;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using StarForged_Claude_MCP.Server.Services;
using StarForged_Claude_MCP.Tests.Server.Integration;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class FolderUploadTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Category = "console_upload";

    [Fact]
    public async Task UploadFolder_WhenTheDocumentAlreadyExists_ShouldReplaceItsContent()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("notes.md", Markdown("Notes", "The original text."));
        await Upload(folder, SummaryMode.None);

        folder.Write("notes.md", Markdown("Notes", "The replacement text."));
        await Upload(folder, SummaryMode.None);

        var documents = await Db.GetDocumentIndex(Category);
        documents.Should().ContainSingle(because: "a second run replaces the document rather than adding another");

        var document = await Db.GetDocument(Category, "notes.md");
        document!.Content.Should().Contain("The replacement text.");
        document.Content.Should().NotContain("The original text.");
    }

    [Fact]
    public async Task UploadFolder_WithSummaryModeNone_ShouldNotPromptAndShouldLeaveSummariesAlone()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "notes.md", "Older content.", summary: "A stored summary");
        folder.Write("notes.md", Markdown("Notes", "Entirely new content."));

        var prompt = await Upload(folder, SummaryMode.None);

        prompt.Asked.Should().BeEmpty();
        (await Db.GetDocument(Category, "notes.md"))!.Summary.Should().Be("A stored summary");
    }

    [Fact]
    public async Task UploadFolder_WithSummaryModeDrop_ShouldClearSummariesWithoutPrompting()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "notes.md", "Older content.", summary: "A stored summary");
        folder.Write("notes.md", Markdown("Notes", "Entirely new content."));

        var prompt = await Upload(folder, SummaryMode.Drop);

        prompt.Asked.Should().BeEmpty();
        (await Db.GetDocument(Category, "notes.md"))!.Summary.Should().BeNull();
    }

    [Fact]
    public async Task UploadFolder_WithSummaryModeMissing_ShouldOnlyPromptWhereThereIsNoSummary()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "has_one.md", "Older content.", summary: "Already summarised");
        await Db.StoreDocument(Category, "has_none.md", "Older content.", summary: null);

        folder.Write("has_one.md", Markdown("One", "Replacement content."));
        folder.Write("has_none.md", Markdown("None", "Replacement content."));
        folder.Write("brand_new.md", Markdown("New", "Never stored before."));

        var prompt = await Upload(folder, SummaryMode.Missing, answers: new()
        {
            ["has_none.md"] = "Typed for has_none",
            ["brand_new.md"] = "Typed for brand_new"
        });

        prompt.Asked.Should().BeEquivalentTo(["has_none.md", "brand_new.md"],
            because: "a document that already has a summary is left alone in this mode");

        (await Db.GetDocument(Category, "has_one.md"))!.Summary.Should().Be("Already summarised");
        (await Db.GetDocument(Category, "has_none.md"))!.Summary.Should().Be("Typed for has_none");
        (await Db.GetDocument(Category, "brand_new.md"))!.Summary.Should().Be("Typed for brand_new");
    }

    [Fact]
    public async Task UploadFolder_WithSummaryModeAll_ShouldPromptForEveryFileAndOfferTheStoredSummary()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "notes.md", "Older content.", summary: "The stored summary");
        folder.Write("notes.md", Markdown("Notes", "Replacement content."));

        var prompt = await Upload(folder, SummaryMode.All, answers: new() { ["notes.md"] = "A freshly typed summary" });

        prompt.Asked.Should().BeEquivalentTo(["notes.md"]);
        prompt.Offered.Should().BeEquivalentTo(["The stored summary"],
            because: "the mode offers the stored summary so it can be kept by entering nothing");
        (await Db.GetDocument(Category, "notes.md"))!.Summary.Should().Be("A freshly typed summary");
    }

    [Fact]
    public async Task UploadFolder_WhenReplacingAnIndexedDocument_ShouldReindexFromTheNewContent()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("reef.md", Markdown("Reef", "The coral reef teems with colourful tropical fish."));
        await Upload(folder, SummaryMode.None, indexed: true);

        folder.Write("reef.md", Markdown("Forge", "The blacksmith hammered the glowing iron on the anvil."));
        await Upload(folder, SummaryMode.None, indexed: true);

        var results = await Search("coral reef tropical fish");

        results.Should().ContainSingle(because: "the replaced content leaves exactly one chunk behind");
        results[0].Text.Should().Contain("blacksmith",
            because: "re-indexing discards the chunks of the content that was replaced");
    }

    [Fact]
    public async Task UploadFolder_WhenIndexingIsTurnedOffOnReplacement_ShouldRemoveWhatWasIndexed()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("reef.md", Markdown("Reef", "The coral reef teems with colourful tropical fish."));
        await Upload(folder, SummaryMode.None, indexed: true);

        await Upload(folder, SummaryMode.None, indexed: false);

        (await Search("coral reef tropical fish")).Should()
            .BeEmpty(because: "a replacement without --index leaves nothing searchable behind");
    }

    private static string Markdown(string header, string body) =>
        $"# {header}{Environment.NewLine}{Environment.NewLine}{body}";

    private async Task<RecordingSummaryPrompt> Upload(
        TempFolder folder,
        SummaryMode summaries,
        bool indexed = false,
        Dictionary<string, string>? answers = null)
    {
        var prompt = new RecordingSummaryPrompt(answers ?? []);

        var uploader = new FileUploader(
            _fixture.Services.GetRequiredService<IDocumentProcessingService>(),
            Db,
            new BeatPreprocessor(),
            prompt);

        await uploader.UploadFile(
            new UploadOptions(Category, UploadMode.Folder, FolderPath: folder.Path, Indexed: indexed, Summaries: summaries),
            CancellationToken.None);

        return prompt;
    }

    private async Task<SearchResult[]> Search(string query) =>
        await _fixture.Services.GetRequiredService<IEmbeddingsFacade>().SearchAsync(query, Category, topK: 10);

    /// <summary>
    /// Answers prompts by filename and records what it was asked. Keyed by filename rather than
    /// ordered, because the order files are prompted for is whatever Directory.GetFiles returns.
    /// </summary>
    private sealed class RecordingSummaryPrompt(Dictionary<string, string> answers) : ISummaryPrompt
    {
        public List<string> Asked { get; } = [];
        public List<string?> Offered { get; } = [];

        public string? Ask(string filename, string? existingSummary)
        {
            Asked.Add(filename);
            Offered.Add(existingSummary);

            return answers.TryGetValue(filename, out var answer) ? answer : existingSummary;
        }
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sf_upload_" + Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(Path);

        public void Write(string filename, string content) =>
            File.WriteAllText(System.IO.Path.Combine(Path, filename), content);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
        }
    }
}
