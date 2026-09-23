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
        output.Should().Contain("Downloaded 3 document(s)").And.Contain("2 new, 1 overwritten.");
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
        withoutOverwrite.Should().Contain("Downloaded 1 document(s) from 2 categories").And.Contain("1 new, 0 overwritten.");
        withoutOverwrite.Should().Contain("Skipped 2 existing file(s)");

        var withOverwrite = await DownloadCapturingOutput(new DownloadOptions(Category, folder.Path, DownloadMode.Folder, Overwrite: true));
        withOverwrite.Should().Contain("Downloaded 3 document(s) from 2 categories").And.Contain("0 new, 3 overwritten.");
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
    public async Task DownloadDocument_ShouldReportWhetherTheFileWasNewOrOverwritten()
    {
        await ClearTestDocuments();
        using var folder = new TempFolder();

        await Db.StoreDocument(Category, "ship.md", "The ship.", summary: null);
        var target = Path.Combine(folder.Path, "ship.md");
        var options = new DownloadOptions(Category, target, DownloadMode.Document, Filename: "ship.md", Overwrite: true);

        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (new)");
        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (overwritten)");
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
    public async Task DownloadBeats_ShouldReportWhetherTheFileWasNewOrOverwritten()
    {
        await ClearTestBeats();
        using var folder = new TempFolder();

        await Db.StoreBeat(Category, sessionNumber: 3, beatNumber: 1, version: 0, content: "Beat 1.0 The arrival.");
        var target = Path.Combine(folder.Path, "session3.md");
        var options = new DownloadOptions(Category, target, DownloadMode.Beats, SessionNumber: 3, Overwrite: true);

        (await DownloadCapturingOutput(options)).Should().Contain($"{target} (new).");
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

    private async Task Download(DownloadOptions options) =>
        await new FileDownloader(Db).DownloadFile(options);

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
}
