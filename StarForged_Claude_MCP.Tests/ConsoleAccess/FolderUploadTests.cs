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

        prompt.Summary.Asked.Should().BeEmpty();
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

        prompt.Summary.Asked.Should().BeEmpty();
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

        prompt.Summary.Asked.Should().BeEquivalentTo(["has_none.md", "brand_new.md"],
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

        prompt.Summary.Asked.Should().BeEquivalentTo(["notes.md"]);
        prompt.Summary.Offered.Should().BeEquivalentTo(["The stored summary"],
            because: "the mode offers the stored summary so it can be kept by entering nothing");
        (await Db.GetDocument(Category, "notes.md"))!.Summary.Should().Be("A freshly typed summary");
    }

    [Fact]
    public async Task UploadFolder_WhenReplacingAnIndexedDocument_ShouldReindexFromTheNewContent()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("reef.md", Markdown("Reef", "The coral reef teems with colourful tropical fish."));
        await Upload(folder, SummaryMode.None, index: IndexMode.All);

        folder.Write("reef.md", Markdown("Forge", "The blacksmith hammered the glowing iron on the anvil."));
        var prompt = await Upload(folder, SummaryMode.None, index: IndexMode.All);

        prompt.Index.Asked.Should().BeEmpty();

        var results = await Search("coral reef tropical fish");

        results.Should().ContainSingle(because: "the replaced content leaves exactly one chunk behind");
        results[0].Text.Should().Contain("blacksmith",
            because: "re-indexing discards the chunks of the content that was replaced");
    }

    [Fact]
    public async Task UploadFolder_WithIndexModeDrop_ShouldRemoveWhatWasIndexedWithoutPrompting()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("reef.md", Markdown("Reef", "The coral reef teems with colourful tropical fish."));
        await Upload(folder, SummaryMode.None, index: IndexMode.All);

        var prompt = await Upload(folder, SummaryMode.None, index: IndexMode.Drop);

        prompt.Index.Asked.Should().BeEmpty();
        (await Search("coral reef tropical fish")).Should()
            .BeEmpty(because: "dropping the index leaves nothing searchable behind");
    }

    [Fact(Timeout = 60_000)]
    public async Task UploadFolder_WithAccentedAndUnknownCharacters_ShouldIndexWithoutHanging()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("games.md", Markdown("Oracle: Favorite Video Game", "63 Planet Zoo\n64 Pokémon\n65 Portal"));
        folder.Write("lunch.md", Markdown("Oracle: Random Food (Lunch)", "29 Smørrebrød with a side of Bún bò Huế"));

        // Run off the test thread so the timeout can fail the test if the tokenizer ever loops again.
        await Task.Run(() => Upload(folder, SummaryMode.None, index: IndexMode.All), TestContext.Current.CancellationToken);

        (await Db.GetDocument(Category, "games.md"))!.Content.Should().Contain("Pokémon",
            because: "only what is tokenized is normalized, never the stored document");
        (await Search("pokemon video game")).Should().Contain(result => result.Text.Contains("Pokémon"),
            because: "the chunk text keeps its accents too");
    }

    [Fact]
    public async Task UploadFolder_WithIndexModeNew_ShouldOnlyPromptForNewFilesAndLeaveStoredOnesAsTheyAre()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("reef.md", Markdown("Reef", "The coral reef teems with colourful tropical fish."));
        folder.Write("forge.md", Markdown("Forge", "The blacksmith hammered the glowing iron."));
        await Upload(folder, SummaryMode.None, index: IndexMode.Ask, indexAnswers: new() { ["reef.md"] = true });

        folder.Write("tundra.md", Markdown("Tundra", "Snow drifts across the frozen tundra."));
        var prompt = await Upload(folder, SummaryMode.None, index: IndexMode.New, indexAnswers: new() { ["tundra.md"] = true });

        prompt.Index.Asked.Should().BeEquivalentTo(["tundra.md"],
            because: "a stored document keeps whether it was indexed in this mode");

        (await Db.GetDocument(Category, "reef.md"))!.Indexed.Should().BeTrue();
        (await Db.GetDocument(Category, "forge.md"))!.Indexed.Should().BeFalse();
        (await Db.GetDocument(Category, "tundra.md"))!.Indexed.Should().BeTrue();
    }

    [Fact]
    public async Task UploadFolder_WithIndexModeAsk_ShouldPromptForEveryFileAndOfferWhetherItIsIndexed()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("reef.md", Markdown("Reef", "The coral reef teems with colourful tropical fish."));
        folder.Write("forge.md", Markdown("Forge", "The blacksmith hammered the glowing iron."));
        await Upload(folder, SummaryMode.None, index: IndexMode.Ask, indexAnswers: new() { ["reef.md"] = true });

        folder.Write("tundra.md", Markdown("Tundra", "Snow drifts across the frozen tundra."));
        var prompt = await Upload(folder, SummaryMode.None, index: IndexMode.Ask, indexAnswers: new() { ["forge.md"] = true });

        prompt.Index.Asked.Should().BeEquivalentTo(["reef.md", "forge.md", "tundra.md"]);
        prompt.Index.Offered.Should().BeEquivalentTo(new bool?[] { true, false, null },
            because: "the mode offers whether a stored document is indexed so it can be kept by entering nothing");

        (await Db.GetDocument(Category, "reef.md"))!.Indexed.Should().BeTrue();
        (await Db.GetDocument(Category, "forge.md"))!.Indexed.Should().BeTrue();
        (await Db.GetDocument(Category, "tundra.md"))!.Indexed.Should().BeFalse();
    }

    [Fact]
    public async Task UploadFolder_WhenNothingChanged_ShouldReportTheDocumentUnchanged()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("reef.md", Markdown("Reef", "The coral reef teems with colourful tropical fish."));
        folder.Write("forge.md", Markdown("Forge", "The blacksmith hammered the glowing iron."));
        await Upload(folder, SummaryMode.None, index: IndexMode.All);

        folder.Write("forge.md", Markdown("Forge", "The blacksmith quenched the glowing iron."));
        var output = await UploadCapturingOutput(folder, SummaryMode.None, index: IndexMode.All);

        output.Should().Contain("Unchanged.");
        output.Should().Contain("0 document(s) stored, 1 replaced, 1 unchanged.");
        (await Search("coral reef tropical fish")).Should().Contain(result => result.Text.Contains("coral reef"),
            because: "an unchanged document keeps its index");
    }

    [Fact]
    public async Task UploadFolder_WhenOnlyTheSummaryChanged_ShouldReplaceIt()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("notes.md", Markdown("Notes", "The notes."));
        await Upload(folder, SummaryMode.None);

        var output = await UploadCapturingOutput(folder, SummaryMode.All, answers: new() { ["notes.md"] = "A new summary" });

        output.Should().Contain("Replaced: summary changed.");
        output.Should().Contain("1 replaced, 0 unchanged.");
        (await Db.GetDocument(Category, "notes.md"))!.Summary.Should().Be("A new summary");
    }

    [Fact]
    public async Task UploadFolder_WhenOnlyIndexingIsTurnedOn_ShouldIndexTheExistingContent()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("reef.md", Markdown("Reef", "The coral reef teems with colourful tropical fish."));
        await Upload(folder, SummaryMode.None);

        var output = await UploadCapturingOutput(folder, SummaryMode.None, index: IndexMode.All);

        output.Should().Contain("Replaced: indexed.");
        (await Search("coral reef tropical fish")).Should().NotBeEmpty();
    }

    [Fact]
    public async Task UploadDocument_ShouldStoreOnlyThatFileAndApplyTheSummaryMode()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("chosen.md", Markdown("Chosen", "The one to upload."));
        folder.Write("sibling.md", Markdown("Sibling", "Left where it is."));

        var prompt = await Run(
            new UploadOptions(Category, UploadMode.Document, SourcePath: Path.Combine(folder.Path, "chosen.md")),
            answers: new() { ["chosen.md"] = "Typed for chosen" });

        prompt.Summary.Asked.Should().BeEquivalentTo(["chosen.md"]);

        var documents = await Db.GetDocumentIndex(Category);
        documents.Should().ContainSingle(because: "only the named file is uploaded, not the rest of its folder")
            .Which.Filename.Should().Be("chosen.md");
        (await Db.GetDocument(Category, "chosen.md"))!.Summary.Should().Be("Typed for chosen");
    }

    [Fact]
    public async Task UploadFolder_IntoAParentCategory_ShouldStoreNothing()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument("Campaign.Oracles", "moons.md", "# Moons", summary: null);
        folder.Write("overview.md", Markdown("Overview", "The campaign at a glance."));

        await Run(new UploadOptions("Campaign", UploadMode.Folder, SourcePath: folder.Path, Index: IndexMode.Drop, Summaries: SummaryMode.None));

        (await Db.GetDocumentIndex("Campaign")).Should().BeEmpty(because: "a category holds either documents or subcategories");
    }

    [Fact]
    public async Task UploadFolder_UnderACategoryThatHoldsDocuments_ShouldStoreNothing()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument("Campaign.Oracles", "moons.md", "# Moons", summary: null);
        folder.Write("red_moon.md", Markdown("The Red Moon", "It rises last."));

        await Run(new UploadOptions("Campaign.Oracles.Moons", UploadMode.Folder, SourcePath: folder.Path, Index: IndexMode.Drop, Summaries: SummaryMode.None));

        (await Db.GetDocumentIndex("Campaign.Oracles.Moons")).Should().BeEmpty();
    }

    [Fact]
    public async Task UploadFolder_WithSubfolders_ShouldStoreEachAsANestedSubcategory()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write(Path.Combine("Oracles", "moves.md"), Markdown("Moves", "The moves."));
        folder.Write(Path.Combine("Npcs", "Allies", "kira.md"), Markdown("Kira", "An ally."));
        folder.Write(Path.Combine("Npcs", "Rivals", "vex.md"), Markdown("Vex", "A rival."));

        await Upload(folder, SummaryMode.None);

        (await Db.GetCategoriesUnder(Category)).Should().Equal(
            $"{Category}.Npcs.Allies", $"{Category}.Npcs.Rivals", $"{Category}.Oracles");
        (await Db.GetDocument($"{Category}.Npcs.Allies", "kira.md"))!.Content.Should().Contain("An ally.");
        (await Db.GetDocumentIndex(Category)).Should().BeEmpty(because: "the root folder held only subfolders");
    }

    [Fact]
    public async Task UploadFolder_ShouldSkipFoldersStartingWithAPeriodAndFoldersWithoutMarkdown()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write("notes.md", Markdown("Notes", "The notes."));
        folder.Write(Path.Combine(".git", "description.md"), "Not part of the upload.");
        folder.Write(Path.Combine("images", "map.png"), "Not markdown.");

        await Upload(folder, SummaryMode.None);

        (await Db.GetCategoriesUnder(Category)).Should().BeEmpty();
        (await Db.GetDocumentIndex(Category)).Select(entry => entry.Filename).Should().Equal("notes.md");
    }

    [Fact]
    public async Task UploadFolder_WhenAFolderHoldsBothFilesAndSubfolders_ShouldStoreNothing()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write(Path.Combine("Oracles", "moves.md"), Markdown("Moves", "The moves."));
        folder.Write(Path.Combine("Npcs", "overview.md"), Markdown("Overview", "Everyone."));
        folder.Write(Path.Combine("Npcs", "Allies", "kira.md"), Markdown("Kira", "An ally."));

        await Upload(folder, SummaryMode.None);

        (await Db.GetCategoriesUnder(Category)).Should().BeEmpty(because: "the whole folder is checked before anything is written");
    }

    [Fact]
    public async Task UploadFolder_WhenASubfolderNameContainsAPeriod_ShouldStoreNothing()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        folder.Write(Path.Combine("Oracles", "moves.md"), Markdown("Moves", "The moves."));
        folder.Write(Path.Combine("v1.2", "notes.md"), Markdown("Notes", "The notes."));

        await Upload(folder, SummaryMode.None);

        (await Db.GetCategoriesUnder(Category)).Should().BeEmpty();
    }

    [Fact]
    public async Task UploadFolder_WhenASubfolderClashesWithStoredCategories_ShouldStoreNothing()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument($"{Category}.Npcs.Allies", "kira.md", "Kira.", summary: null);
        folder.Write(Path.Combine("Oracles", "moves.md"), Markdown("Moves", "The moves."));
        folder.Write(Path.Combine("Npcs", "vex.md"), Markdown("Vex", "A rival."));

        await Upload(folder, SummaryMode.None);

        (await Db.GetCategoriesUnder(Category)).Should().Equal(
            [$"{Category}.Npcs.Allies"], because: "'Npcs' is already a parent category, so nothing is written");
    }

    private static string Markdown(string header, string body) =>
        $"# {header}{Environment.NewLine}{Environment.NewLine}{body}";

    private async Task<Prompts> Upload(
        TempFolder folder,
        SummaryMode summaries,
        IndexMode index = IndexMode.Drop,
        Dictionary<string, string>? answers = null,
        Dictionary<string, bool>? indexAnswers = null) =>
        await Run(
            new UploadOptions(Category, UploadMode.Folder, SourcePath: folder.Path, Index: index, Summaries: summaries),
            answers,
            indexAnswers);

    private async Task<string> UploadCapturingOutput(
        TempFolder folder,
        SummaryMode summaries,
        IndexMode index = IndexMode.Drop,
        Dictionary<string, string>? answers = null)
    {
        var original = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);

        try
        {
            await Upload(folder, summaries, index, answers);
        }
        finally
        {
            Console.SetOut(original);
        }

        return output.ToString();
    }

    private async Task<Prompts> Run(
        UploadOptions options,
        Dictionary<string, string>? answers = null,
        Dictionary<string, bool>? indexAnswers = null)
    {
        var prompts = new Prompts(new RecordingSummaryPrompt(answers ?? []), new RecordingIndexPrompt(indexAnswers ?? []));

        var uploader = new FileUploader(
            _fixture.Services.GetRequiredService<IDocumentProcessingService>(),
            Db,
            new BeatPreprocessor(),
            prompts.Summary,
            prompts.Index);

        await uploader.UploadFile(options, CancellationToken.None);

        return prompts;
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

    /// <summary>
    /// Answers index prompts by filename the way <see cref="RecordingSummaryPrompt"/> does; a file without
    /// an answer keeps whether it is indexed, as a blank answer would.
    /// </summary>
    private sealed class RecordingIndexPrompt(Dictionary<string, bool> answers) : IIndexPrompt
    {
        public List<string> Asked { get; } = [];
        public List<bool?> Offered { get; } = [];

        public bool Ask(string filename, bool? currentlyIndexed)
        {
            Asked.Add(filename);
            Offered.Add(currentlyIndexed);

            return answers.TryGetValue(filename, out var answer) ? answer : currentlyIndexed ?? false;
        }
    }

    private sealed record Prompts(RecordingSummaryPrompt Summary, RecordingIndexPrompt Index);
}
