using FluentAssertions;
using StarForged_Claude_MCP.ConsoleAccess.Download;
using StarForged_Claude_MCP.Tests.Server.Integration;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class DownloadTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Category = "console_download";

    [Fact]
    public async Task DownloadFolder_ShouldWriteEveryDocumentInTheCategory()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "First content.", summary: null);
        await Db.StoreDocument(Category, "two.md", "Second content.", summary: null);
        await Db.StoreDocument("some_other_category", "three.md", "Not in this category.", summary: null);

        var target = Path.Combine(folder.Path, "not_yet_created");
        await Download(new DownloadOptions(Category, target, DownloadMode.Folder));

        Directory.GetFiles(target).Select(Path.GetFileName).Should().BeEquivalentTo(["one.md", "two.md"]);
        File.ReadAllText(Path.Combine(target, "one.md")).Should().Be("First content.");
    }

    [Fact]
    public async Task DownloadFolder_WithoutOverwrite_ShouldSkipExistingFilesButWriteTheRest()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        await Db.StoreDocument(Category, "two.md", "Second content.", summary: null);
        folder.Write("one.md", "Local content.");

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Folder));

        File.ReadAllText(Path.Combine(folder.Path, "one.md")).Should().Be("Local content.");
        File.ReadAllText(Path.Combine(folder.Path, "two.md")).Should().Be("Second content.");
    }

    [Fact]
    public async Task DownloadFolder_WithOverwrite_ShouldReplaceExistingFiles()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        folder.Write("one.md", "Local content.");

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Overwrite: true));

        File.ReadAllText(Path.Combine(folder.Path, "one.md")).Should().Be("Stored content.");
    }

    [Fact]
    public async Task DownloadFolder_ShouldReportHowManyFilesWereNewAndHowManyOverwritten()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        await Db.StoreDocument(Category, "two.md", "Second content.", summary: null);
        await Db.StoreDocument(Category, "three.md", "Third content.", summary: null);
        folder.Write("one.md", "Local content.");

        var output = await DownloadCapturingOutput(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Overwrite: true));

        output.Should().Contain($"{Path.Combine(folder.Path, "one.md")} (overwritten)");
        output.Should().Contain($"{Path.Combine(folder.Path, "two.md")} (new)");
        output.Should().Contain("Downloaded 3 document(s)").And.Contain("2 new, 1 overwritten, 0 unchanged.");
    }

    [Fact]
    public async Task DownloadFolder_WithOverwrite_WhenAFileIsIdentical_ShouldLeaveItUntouchedAndReportItUnchanged()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        await Db.StoreDocument(Category, "two.md", "Second content.", summary: null);
        folder.Write("one.md", "Stored content.");
        folder.Write("two.md", "Local content.");

        var identical = Path.Combine(folder.Path, "one.md");
        var longAgo = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(identical, longAgo);

        var output = await DownloadCapturingOutput(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Overwrite: true));

        output.Should().Contain($"{identical} (unchanged)");
        output.Should().Contain("Downloaded 1 document(s)").And.Contain("0 new, 1 overwritten, 1 unchanged.");
        File.GetLastWriteTimeUtc(identical).Should().Be(longAgo, because: "an identical file is not rewritten");
    }

    [Fact]
    public async Task DownloadFolder_WithoutOverwrite_ShouldCountIdenticalFilesAsUnchangedAndOnlySkipDifferingOnes()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        await Db.StoreDocument(Category, "two.md", "Second content.", summary: null);
        folder.Write("one.md", "Stored content.");
        folder.Write("two.md", "Local content.");

        var output = await DownloadCapturingOutput(new DownloadOptions(Category, folder.Path, DownloadMode.Folder));

        output.Should().Contain($"{Path.Combine(folder.Path, "one.md")} (unchanged)");
        output.Should().Contain("0 new, 0 overwritten, 1 unchanged.");
        output.Should().Contain("Skipped 1 existing file(s) that differ from the stored version");
        File.ReadAllText(Path.Combine(folder.Path, "two.md")).Should().Be("Local content.");
    }

    [Fact]
    public async Task DownloadFolder_OfAParentCategory_ShouldWriteEachLeafIntoItsOwnNestedFolder()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument($"{Category}.Oracles", "moves.md", "The moves.", summary: null);
        await Db.StoreDocument($"{Category}.Npcs.Allies", "kira.md", "Kira.", summary: null);
        await Db.StoreDocument($"{Category}.Npcs.Rivals", "vex.md", "Vex.", summary: null);
        await Db.StoreDocument("some_other_category", "other.md", "Not under this parent.", summary: null);

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Folder));

        Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folder.Path, path))
            .Should().BeEquivalentTo([
                Path.Combine("Oracles", "moves.md"),
                Path.Combine("Npcs", "Allies", "kira.md"),
                Path.Combine("Npcs", "Rivals", "vex.md")]);
        File.ReadAllText(Path.Combine(folder.Path, "Npcs", "Allies", "kira.md")).Should().Be("Kira.");
    }

    [Fact]
    public async Task DownloadFolder_OfAParentCategory_ShouldReportTotalsAcrossEveryLeaf()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument($"{Category}.Oracles", "moves.md", "The moves.", summary: null);
        await Db.StoreDocument($"{Category}.Npcs.Allies", "kira.md", "Kira.", summary: null);
        await Db.StoreDocument($"{Category}.Npcs.Allies", "tam.md", "Tam.", summary: null);
        folder.Write(Path.Combine("Oracles", "moves.md"), "Local moves.");
        folder.Write(Path.Combine("Npcs", "Allies", "kira.md"), "Local Kira.");

        var withoutOverwrite = await DownloadCapturingOutput(new DownloadOptions(Category, folder.Path, DownloadMode.Folder));
        withoutOverwrite.Should().Contain("Downloaded 1 document(s) from 2 categories").And.Contain("1 new, 0 overwritten, 0 unchanged.");
        withoutOverwrite.Should().Contain("Skipped 2 existing file(s)");

        var withOverwrite = await DownloadCapturingOutput(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Overwrite: true));
        withOverwrite.Should().Contain("Downloaded 2 document(s) from 2 categories").And.Contain("0 new, 2 overwritten, 1 unchanged.",
            because: "tam.md was already written by the first download");
        File.ReadAllText(Path.Combine(folder.Path, "Oracles", "moves.md")).Should().Be("The moves.");
    }

    [Fact]
    public async Task DownloadDocument_ToAFilePath_ShouldWriteOnlyThatDocumentThere()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "ship.md", "The ship.", summary: null);
        await Db.StoreDocument(Category, "crew.md", "The crew.", summary: null);

        var target = Path.Combine(folder.Path, "renamed.md");
        await Download(new DownloadOptions(Category, target, DownloadMode.Document, Filename: "ship.md"));

        Directory.GetFiles(folder.Path).Select(Path.GetFileName).Should().BeEquivalentTo(["renamed.md"]);
        File.ReadAllText(target).Should().Be("The ship.");
    }

    [Fact]
    public async Task DownloadDocument_ToAnExistingFolder_ShouldWriteItUnderItsOwnName()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "ship.md", "The ship.", summary: null);

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Document, Filename: "ship.md"));

        File.ReadAllText(Path.Combine(folder.Path, "ship.md")).Should().Be("The ship.");
    }

    [Fact]
    public async Task DownloadDocument_ToAFolderThatDoesNotExistYet_ShouldCreateItAndWriteUnderItsOwnName()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "ship.md", "The ship.", summary: null);

        var target = Path.Combine(folder.Path, "new_folder") + Path.DirectorySeparatorChar;
        await Download(new DownloadOptions(Category, target, DownloadMode.Document, Filename: "ship.md"));

        File.ReadAllText(Path.Combine(target, "ship.md")).Should().Be("The ship.");
    }

    [Fact]
    public async Task DownloadDocument_WhenTheFileExists_ShouldOnlyReplaceItWithOverwrite()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "ship.md", "The ship.", summary: null);
        folder.Write("ship.md", "Local content.");
        var target = Path.Combine(folder.Path, "ship.md");

        await Download(new DownloadOptions(Category, target, DownloadMode.Document, Filename: "ship.md"));
        File.ReadAllText(target).Should().Be("Local content.", because: "an existing file is left alone by default");

        await Download(new DownloadOptions(Category, target, DownloadMode.Document, Filename: "ship.md", Overwrite: true));
        File.ReadAllText(target).Should().Be("The ship.");
    }

    [Fact]
    public async Task DownloadDocument_ShouldReportWhetherTheFileWasNewOverwrittenOrUnchanged()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "ship.md", "The ship.", summary: null);
        var target = Path.Combine(folder.Path, "ship.md");
        var options = new DownloadOptions(Category, target, DownloadMode.Document, Filename: "ship.md", Overwrite: true);

        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (new)");
        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (unchanged)");

        folder.Write("ship.md", "Local content.");
        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (overwritten)");
    }

    [Fact]
    public async Task DownloadDocument_WithoutOverwrite_WhenTheFileIsIdentical_ShouldReportItUnchanged()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "ship.md", "The ship.", summary: null);
        folder.Write("ship.md", "The ship.");
        var target = Path.Combine(folder.Path, "ship.md");

        var output = await DownloadCapturingOutput(new DownloadOptions(Category, target, DownloadMode.Document, Filename: "ship.md"));

        output.Should().Contain($"{target} (unchanged)", because: "an identical file is not a conflict that needs --overwrite");
    }

    [Fact]
    public async Task DownloadDocument_WhenTheDocumentDoesNotExist_ShouldWriteNothing()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Document, Filename: "missing.md"));

        Directory.GetFiles(folder.Path).Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadBeats_ShouldWriteTheCanonicalSessionToOneFile()
    {
        await ClearTestBeats();
        using var folder = new TempFolder();

        await Db.StoreBeat(Category, sessionNumber: 3, beatNumber: 1, version: 0, content: "Beat 1.0 The original arrival.\n");
        await Db.StoreBeat(Category, sessionNumber: 3, beatNumber: null, version: null, content: "Interlude: rivals plotted.\n");
        await Db.StoreBeat(Category, sessionNumber: 3, beatNumber: 1, version: 1, content: "Beat 1.1 The corrected arrival.\n");
        await Db.StoreBeat(Category, sessionNumber: 4, beatNumber: 1, version: 0, content: "Beat 1.0 Another session.\n");

        var target = Path.Combine(folder.Path, "session3.md");
        await Download(new DownloadOptions(Category, target, DownloadMode.Beats, SessionNumber: 3));

        var separator = Environment.NewLine + Environment.NewLine;
        File.ReadAllText(target).Should().Be(
            "Beat 1.1 The corrected arrival." + separator + "Interlude: rivals plotted." + Environment.NewLine,
            because: "a rewritten beat keeps its original place and superseded versions are dropped");
    }

    [Fact]
    public async Task DownloadBeats_WhenTheFileExists_ShouldOnlyReplaceItWithOverwrite()
    {
        await ClearTestBeats();
        using var folder = new TempFolder();

        await Db.StoreBeat(Category, sessionNumber: 3, beatNumber: 1, version: 0, content: "Beat 1.0 The arrival.");
        folder.Write("session3.md", "Local content.");
        var target = Path.Combine(folder.Path, "session3.md");

        await Download(new DownloadOptions(Category, target, DownloadMode.Beats, SessionNumber: 3));
        File.ReadAllText(target).Should().Be("Local content.", because: "an existing file is left alone by default");

        await Download(new DownloadOptions(Category, target, DownloadMode.Beats, SessionNumber: 3, Overwrite: true));
        File.ReadAllText(target).Should().StartWith("Beat 1.0 The arrival.");
    }

    [Fact]
    public async Task DownloadBeats_ShouldReportWhetherTheFileWasNewOverwrittenOrUnchanged()
    {
        await ClearTestBeats();
        using var folder = new TempFolder();

        await Db.StoreBeat(Category, sessionNumber: 3, beatNumber: 1, version: 0, content: "Beat 1.0 The arrival.");
        var target = Path.Combine(folder.Path, "session3.md");
        var options = new DownloadOptions(Category, target, DownloadMode.Beats, SessionNumber: 3, Overwrite: true);

        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (new).");
        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (unchanged).");

        folder.Write("session3.md", "Local content.");
        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (overwritten).");
    }

    [Fact]
    public async Task DownloadBeats_WhenTheSessionHasNoBeats_ShouldWriteNothing()
    {
        await ClearTestBeats();
        using var folder = new TempFolder();

        var target = Path.Combine(folder.Path, "session9.md");
        await Download(new DownloadOptions(Category, target, DownloadMode.Beats, SessionNumber: 9));

        File.Exists(target).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadFolder_WithClean_ShouldListAndDeleteOnlyStrayMarkdownOnceConfirmed()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        folder.Write("stray.md", "Not in the store.");
        folder.Write(Path.Combine("Old", "Deep", "gone.md"), "Not in the store.");
        folder.Write("map.png", "Not markdown.");
        folder.Write(Path.Combine(".obsidian", "workspace.md"), "Inside a dot-folder.");
        confirmPrompt.Answer = true;

        var output = await DownloadCapturingOutput(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Clean: true));

        confirmPrompt.Asked.Should().ContainSingle();
        output.Should().Contain("  stray.md").And.Contain($"  {Path.Combine("Old", "Deep", "gone.md")}");
        output.Should().Contain("Deleted 2 file(s) not in the download, and 2 folder(s) left empty.");

        File.Exists(Path.Combine(folder.Path, "stray.md")).Should().BeFalse();
        Directory.Exists(Path.Combine(folder.Path, "Old")).Should().BeFalse(because: "folders emptied by the clean are removed");
        File.ReadAllText(Path.Combine(folder.Path, "one.md")).Should().Be("Stored content.");
        File.Exists(Path.Combine(folder.Path, "map.png")).Should().BeTrue(because: "only .md files are cleaned, as only those are uploaded");
        File.Exists(Path.Combine(folder.Path, ".obsidian", "workspace.md")).Should().BeTrue(because: "dot-folders are left alone");
    }

    [Fact]
    public async Task DownloadFolder_WithClean_WhenDeclined_ShouldNeitherDownloadNorDelete()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        folder.Write("stray.md", "Not in the store.");
        confirmPrompt.Answer = false;

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Clean: true));

        confirmPrompt.Asked.Should().ContainSingle();
        File.Exists(Path.Combine(folder.Path, "stray.md")).Should().BeTrue();
        File.Exists(Path.Combine(folder.Path, "one.md")).Should().BeFalse(because: "declining cancels the whole download");
    }

    [Fact]
    public async Task DownloadFolder_WithClean_OverTheLimit_ShouldAbortWithoutAsking()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        for (var i = 0; i <= FileDownloader.MaxCleanDeletions; i++)
        {
            folder.Write($"stray{i}.md", "Not in the store.");
        }
        confirmPrompt.Answer = true;

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Clean: true));

        confirmPrompt.Asked.Should().BeEmpty();
        Directory.GetFiles(folder.Path).Should().HaveCount(FileDownloader.MaxCleanDeletions + 1,
            because: "nothing is deleted or downloaded");
    }

    [Fact]
    public async Task DownloadFolder_WithClean_WhenNothingIsStray_ShouldDownloadWithoutAsking()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "one.md", "Stored content.", summary: null);
        folder.Write("one.md", "Local content.");

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Overwrite: true, Clean: true));

        confirmPrompt.Asked.Should().BeEmpty();
        File.ReadAllText(Path.Combine(folder.Path, "one.md")).Should().Be("Stored content.");
    }

    [Fact]
    public async Task DownloadFolder_WithClean_OfAParentCategory_ShouldKeepEveryLeafsFiles()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument($"{Category}.Oracles", "moves.md", "The moves.", summary: null);
        await Db.StoreDocument($"{Category}.Npcs.Allies", "kira.md", "Kira.", summary: null);
        folder.Write(Path.Combine("Npcs", "Allies", "kira.md"), "Local Kira.");
        folder.Write(Path.Combine("Npcs", "Rivals", "vex.md"), "Not in the store.");
        confirmPrompt.Answer = true;

        await Download(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Clean: true));

        File.ReadAllText(Path.Combine(folder.Path, "Npcs", "Allies", "kira.md")).Should().Be("Local Kira.",
            because: "a file skipped for lack of --overwrite is still part of the download");
        File.Exists(Path.Combine(folder.Path, "Oracles", "moves.md")).Should().BeTrue();
        Directory.Exists(Path.Combine(folder.Path, "Npcs", "Rivals")).Should().BeFalse();
    }

    private readonly RecordingConfirmPrompt confirmPrompt = new();

    private async Task Download(DownloadOptions options) =>
        await new FileDownloader(Db, confirmPrompt).DownloadFile(options);

    private async Task<string> DownloadCapturingOutput(DownloadOptions options)
    {
        var original = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);

        try
        {
            await Download(options);
        }
        finally
        {
            Console.SetOut(original);
        }

        return output.ToString();
    }

    private sealed class RecordingConfirmPrompt : IConfirmPrompt
    {
        public bool Answer { get; set; }
        public List<string> Asked { get; } = [];

        public bool Confirm(string question)
        {
            Asked.Add(question);
            return Answer;
        }
    }
}
