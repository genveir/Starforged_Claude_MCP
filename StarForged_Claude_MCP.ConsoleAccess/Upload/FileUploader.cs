using StarForged_Claude_MCP.Database;
using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services;

namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public class FileUploader
{
    private readonly IDocumentProcessingService documentProcessingService;
    private readonly DbInterface dbInterface;
    private readonly ISummaryPrompt summaryPrompt;
    private readonly IIndexPrompt indexPrompt;

    public FileUploader(
        IDocumentProcessingService documentProcessingService,
        DbInterface dbInterface,
        ISummaryPrompt summaryPrompt,
        IIndexPrompt indexPrompt)
    {
        this.documentProcessingService = documentProcessingService;
        this.dbInterface = dbInterface;
        this.summaryPrompt = summaryPrompt;
        this.indexPrompt = indexPrompt;
    }

    public async Task UploadFile(UploadOptions options)
    {
        if (options.DryRun)
        {
            Console.WriteLine("Dry run: nothing will be stored, replaced or indexed, and nothing will be asked.");
        }

        switch (options.Mode)
        {
            case UploadMode.Folder:
                if (!Directory.Exists(options.SourcePath))
                {
                    Console.Error.WriteLine($"Error: Folder '{options.SourcePath}' does not exist.");
                    return;
                }
                await UploadFolderAsync(
                    options.Category, options.SourcePath, options.Index, options.Summaries, options.DryRun, options.Verbosity);
                break;
            case UploadMode.Document:
                if (!File.Exists(options.SourcePath))
                {
                    Console.Error.WriteLine($"Error: File '{options.SourcePath}' does not exist.");
                    return;
                }
                if (!await CategoryHierarchy.RequireCanHoldDocuments(dbInterface, options.Category)) return;
                await UploadFilesAsync(
                    options.Category, [options.SourcePath], options.Index, options.Summaries, options.DryRun, options.Verbosity,
                    isOutermost: true);
                break;
            default:
                throw new ArgumentException($"Invalid upload mode {options.Mode}");
        }
    }

    /// <summary>
    /// Stores the .md files of <paramref name="folderPath"/> under <paramref name="category"/>. Each subfolder
    /// becomes a subcategory, at any depth, so 'Oracles/moves.md' is stored in '{category}.Oracles'. The whole
    /// folder is checked before anything is written, so a folder that breaks the category hierarchy stores nothing.
    /// </summary>
    private async Task UploadFolderAsync(
        string category, string folderPath, IndexMode index, SummaryMode summaries, bool dryRun, Verbosity verbosity)
    {
        var leaves = new List<LeafUpload>();
        if (!TryPlanFolder(category, folderPath, leaves)) return;

        if (leaves.Count == 0)
        {
            Console.Error.WriteLine($"No .md files found in '{folderPath}'.");
            return;
        }

        foreach (var leaf in leaves)
        {
            if (!await CategoryHierarchy.RequireCanHoldDocuments(dbInterface, leaf.Category)) return;
        }

        if (leaves is [var only] && only.Category == category)
        {
            await UploadFilesAsync(category, only.Files, index, summaries, dryRun, verbosity, isOutermost: true);
            return;
        }

        var total = new UploadTally();
        foreach (var leaf in leaves)
        {
            total += await UploadFilesAsync(leaf.Category, leaf.Files, index, summaries, dryRun, verbosity, isOutermost: false);
        }

        Console.WriteLine(
            $"\n{(dryRun ? "Would upload" : "Uploaded")} to {leaves.Count} categor{(leaves.Count == 1 ? "y" : "ies")} " +
            $"under '{category}': {total.Stored} stored, {total.Replaced} replaced, {total.Unchanged} unchanged.");
        ReportSkippedQuestions(total.SkippedQuestions);
    }

    /// <summary>
    /// Adds a <see cref="LeafUpload"/> to <paramref name="leaves"/> for every folder that holds .md files, walking
    /// every subfolder whose name does not start with a period. Subfolders without any .md files in them are
    /// ignored. Reports the first folder that cannot become a category on stderr and returns false.
    /// </summary>
    private static bool TryPlanFolder(string category, string folderPath, List<LeafUpload> leaves)
    {
        var leavesBefore = leaves.Count;

        var subfolders = Directory.GetDirectories(folderPath)
            .Where(subfolder => !Path.GetFileName(subfolder).StartsWith('.'))
            .OrderBy(subfolder => subfolder, StringComparer.OrdinalIgnoreCase);

        foreach (var subfolder in subfolders)
        {
            var name = Path.GetFileName(subfolder);
            var leavesBeforeSubfolder = leaves.Count;

            if (!TryPlanFolder($"{category}{CategoryPath.Separator}{name}", subfolder, leaves)) return false;

            var becomesCategory = leaves.Count > leavesBeforeSubfolder;
            if (becomesCategory && (name.Contains(CategoryPath.Separator) || !CategoryPath.IsWellFormed(name)))
            {
                Console.Error.WriteLine(
                    $"Error: the folder '{subfolder}' cannot be a category; its name may not contain '{CategoryPath.Separator}'.");
                return false;
            }
        }

        var files = Directory.GetFiles(folderPath, "*.md");
        if (files.Length == 0) return true;

        if (leaves.Count > leavesBefore)
        {
            Console.Error.WriteLine(
                $"Error: '{folderPath}' holds both .md files and subfolders with .md files; " +
                "a category holds either documents or subcategories, never both.");
            return false;
        }

        leaves.Add(new LeafUpload(category, files));
        return true;
    }

    /// <summary>
    /// Stores <paramref name="files"/> under <paramref name="category"/>. With <paramref name="dryRun"/> nothing is
    /// written and nothing is asked: every question is taken as answered blank, and the report says what would change.
    /// Each file is reported once it has been handled, so any question about it comes before its line; with
    /// <see cref="Verbosity.Changed"/> the files left unchanged are counted but not reported, and the category's
    /// totals are reported only when something was stored or replaced, unless it is the outermost category of the upload.
    /// </summary>
    private async Task<UploadTally> UploadFilesAsync(
        string category, string[] files, IndexMode index, SummaryMode summaries, bool dryRun, Verbosity verbosity,
        bool isOutermost)
    {
        if (verbosity == Verbosity.All)
        {
            Console.WriteLine($"Found {files.Length} file(s) to process for '{category}'.");
        }

        var stored = 0;
        var replaced = 0;
        var unchanged = 0;
        var skippedQuestions = 0;

        foreach (var filePath in files)
        {
            var text = await File.ReadAllTextAsync(filePath);
            var filename = Path.GetFileName(filePath);

            var existing = await dbInterface.GetDocument(category, filename);

            // Asked for before anything is written, so that abandoning a run part way through
            // never leaves a document stored without the answers that were being typed for it.
            var skipped = new SkippedPrompts();
            var summary = ResolveSummary(filename, existing?.Summary, summaries, dryRun ? skipped : summaryPrompt);
            var indexed = ResolveIndexed(filename, existing?.Indexed, index, dryRun ? skipped : indexPrompt);

            skippedQuestions += skipped.Questions.Count;

            string outcome;
            if (existing == null)
            {
                if (!dryRun) await StoreDocumentAsync(category, filename, text, summary, indexed);
                outcome = (dryRun, indexed) switch
                {
                    (true, true) => "would be stored and indexed",
                    (true, false) => "would be stored",
                    (false, true) => "stored and indexed",
                    (false, false) => "stored"
                };
                stored++;
            }
            else
            {
                var changes = await ReplaceDocumentAsync(existing, text, summary, indexed, dryRun);
                if (changes.Count == 0)
                {
                    unchanged++;
                    if (verbosity == Verbosity.Changed) continue;

                    outcome = "unchanged";
                }
                else
                {
                    outcome = $"{(dryRun ? "would be replaced" : "replaced")}: {string.Join(", ", changes)}";
                    replaced++;
                }
            }

            Console.WriteLine($"{filename} -> {category} ({outcome})");
            if (skipped.Questions.Count > 0)
            {
                Console.WriteLine($"  Would ask for {string.Join(" and ", skipped.Questions)}; taken as answered blank.");
            }
        }

        if (verbosity == Verbosity.All || isOutermost || stored + replaced > 0)
        {
            Console.WriteLine(dryRun
                ? $"\nDry run of '{category}': {stored} document(s) would be stored, {replaced} replaced, {unchanged} unchanged."
                : $"\nCompleted '{category}'! {stored} document(s) stored, {replaced} replaced, {unchanged} unchanged.");
            ReportSkippedQuestions(skippedQuestions);
        }

        return new UploadTally(stored, replaced, unchanged, skippedQuestions);
    }

    private static void ReportSkippedQuestions(int skippedQuestions)
    {
        if (skippedQuestions > 0)
        {
            Console.WriteLine(
                $"{skippedQuestions} question(s) were not asked and taken as answered blank; a real run would ask them.");
        }
    }

    private static string? ResolveSummary(string filename, string? existingSummary, SummaryMode summaries, ISummaryPrompt prompt) => summaries switch
    {
        SummaryMode.All => prompt.Ask(filename, existingSummary),
        SummaryMode.Missing => existingSummary ?? prompt.Ask(filename, existingSummary: null),
        SummaryMode.None => existingSummary,
        SummaryMode.Drop => null,
        _ => throw new ArgumentException($"Unknown summary mode {summaries}", nameof(summaries))
    };

    private static bool ResolveIndexed(string filename, bool? currentlyIndexed, IndexMode index, IIndexPrompt prompt) => index switch
    {
        IndexMode.All => true,
        IndexMode.Ask => prompt.Ask(filename, currentlyIndexed),
        IndexMode.New => currentlyIndexed ?? prompt.Ask(filename, currentlyIndexed: null),
        IndexMode.Drop => false,
        _ => throw new ArgumentException($"Unknown index mode {index}", nameof(index))
    };

    private async Task<int> StoreDocumentAsync(string category, string filename, string content, string? summary, bool indexed)
    {
        var id = await dbInterface.StoreDocument(category, filename, content, summary);

        if (indexed)
        {
            await documentProcessingService.IndexDocumentAsync(content, id, DocumentProcessorToUse.Markdown);
        }

        return id;
    }

    /// <summary>
    /// Brings <paramref name="existing"/> in line with the given content, summary and index state, writing only
    /// what differs: the index is rebuilt only when the content changed or the document was not indexed before.
    /// With <paramref name="dryRun"/> the changes are worked out but not made.
    /// </summary>
    /// <returns>A description of each change made; empty when the document already matched.</returns>
    private async Task<List<string>> ReplaceDocumentAsync(
        Document existing, string content, string? summary, bool indexed, bool dryRun)
    {
        var changes = new List<string>();
        var contentChanged = existing.Content != content;
        var summaryChanged = existing.Summary != summary;

        if (contentChanged) changes.Add("content changed");
        if (summaryChanged) changes.Add("summary changed");

        if ((contentChanged || summaryChanged) && !dryRun)
        {
            await dbInterface.UpdateDocument(existing.Id, content, summary);
        }

        if (indexed && (contentChanged || !existing.Indexed))
        {
            if (!dryRun) await documentProcessingService.IndexDocumentAsync(content, existing.Id, DocumentProcessorToUse.Markdown);
            changes.Add(existing.Indexed ? "reindexed" : "indexed");
        }
        else if (!indexed && existing.Indexed)
        {
            if (!dryRun) await documentProcessingService.RemoveIndexForDocumentAsync(existing.Id);
            changes.Add("removed from index");
        }

        return changes;
    }

    private sealed record LeafUpload(string Category, string[] Files);

    private readonly record struct UploadTally(int Stored, int Replaced, int Unchanged, int SkippedQuestions)
    {
        public static UploadTally operator +(UploadTally left, UploadTally right) => new(
            left.Stored + right.Stored,
            left.Replaced + right.Replaced,
            left.Unchanged + right.Unchanged,
            left.SkippedQuestions + right.SkippedQuestions);
    }

    /// <summary>
    /// Stands in for the prompts on a dry run: records each question instead of asking it, and gives the answer a
    /// blank entry would, which keeps what is stored and leaves a new document without a summary and unindexed.
    /// </summary>
    private sealed class SkippedPrompts : ISummaryPrompt, IIndexPrompt
    {
        public List<string> Questions { get; } = [];

        public string? Ask(string filename, string? existingSummary)
        {
            Questions.Add("a summary");
            return existingSummary;
        }

        public bool Ask(string filename, bool? currentlyIndexed)
        {
            Questions.Add("whether to index");
            return currentlyIndexed ?? false;
        }
    }
}
