using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services;

namespace StarForged_Claude_MCP.ConsoleAccess.Download;

public class FileDownloader
{
    public const int MaxCleanDeletions = 100;

    private readonly DbInterface dbInterface;
    private readonly IConfirmPrompt confirmPrompt;

    public FileDownloader(DbInterface dbInterface, IConfirmPrompt confirmPrompt)
    {
        this.dbInterface = dbInterface;
        this.confirmPrompt = confirmPrompt;
    }

    public async Task DownloadFile(DownloadOptions options)
    {
        switch (options.Mode)
        {
            case DownloadMode.Folder:
                await DownloadFolderAsync(options.Category, options.TargetPath, options.Overwrite, options.Clean);
                break;
            case DownloadMode.Document:
                await DownloadDocumentAsync(options.Category, options.Filename!, options.TargetPath, options.Overwrite);
                break;
            case DownloadMode.Beats:
                await DownloadBeatsAsync(options.Category, options.SessionNumber, options.TargetPath, options.Overwrite);
                break;
            default:
                throw new ArgumentException($"Invalid download mode {options.Mode}");
        }
    }

    /// <summary>
    /// Downloads every document in <paramref name="category"/> into <paramref name="folderPath"/>. For a parent
    /// category every leaf gets its own folder, nested to mirror the category tree: leaf 'Campaign.Npcs.Allies'
    /// under 'Campaign' goes to 'Npcs/Allies'. With <paramref name="clean"/>, the .md files in the folder that are
    /// not part of the download are deleted afterwards, once the user has confirmed the list of them.
    /// </summary>
    private async Task DownloadFolderAsync(string category, string folderPath, bool overwrite, bool clean)
    {
        var leaves = await PlanFolderAsync(category, folderPath);
        if (leaves.Count == 0)
        {
            Console.Error.WriteLine($"No documents found for category '{category}'.");
            return;
        }

        var toDelete = clean ? FindFilesOutsideDownload(folderPath, leaves) : [];
        if (!ConfirmClean(folderPath, toDelete)) return;

        if (leaves is [var only] && only.Category == category)
        {
            var tally = await DownloadLeafAsync(only, overwrite);
            ReportSkipped(tally.Skipped);
        }
        else
        {
            var total = new DownloadTally();
            foreach (var leaf in leaves)
            {
                total += await DownloadLeafAsync(leaf, overwrite);
            }

            Console.WriteLine(
                $"Downloaded {total.Written} document(s) from {leaves.Count} categor{(leaves.Count == 1 ? "y" : "ies")} " +
                $"under '{category}' to {folderPath}: {total.Created} new, {total.Overwritten} overwritten, {total.Unchanged} unchanged.");
            ReportSkipped(total.Skipped);
        }

        DeleteFiles(folderPath, toDelete);
    }

    private async Task<List<LeafDownload>> PlanFolderAsync(string category, string folderPath)
    {
        var leaves = await dbInterface.GetCategoriesUnder(category);
        if (leaves.Count == 0)
        {
            var index = await dbInterface.GetDocumentIndex(category);
            return index.Count == 0 ? [] : [new LeafDownload(category, folderPath, index)];
        }

        var plan = new List<LeafDownload>();
        foreach (var leaf in leaves)
        {
            var relativeSegments = leaf[(category.Length + 1)..].Split(CategoryPath.Separator).Select(SanitiseFileName);
            var leafFolder = Path.Combine([folderPath, .. relativeSegments]);

            plan.Add(new LeafDownload(leaf, leafFolder, await dbInterface.GetDocumentIndex(leaf)));
        }

        return plan;
    }

    /// <summary>
    /// Lists the .md files under <paramref name="folderPath"/> that no document of <paramref name="leaves"/> would be
    /// written to, which is the same set of files an upload of the folder would read. Folders starting with a period
    /// and links to other folders are never looked into.
    /// </summary>
    private static List<string> FindFilesOutsideDownload(string folderPath, List<LeafDownload> leaves)
    {
        var outside = new List<string>();
        if (!Directory.Exists(folderPath)) return outside;

        // Case-insensitive, like the Windows file system, so a file differing only in case is kept rather than deleted.
        var downloaded = leaves
            .SelectMany(leaf => leaf.Index.Select(entry => Path.GetFullPath(leaf.PathFor(entry))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Collect(folderPath);
        return outside;

        void Collect(string folder)
        {
            outside.AddRange(Directory.GetFiles(folder, "*.md")
                .Where(file => !downloaded.Contains(Path.GetFullPath(file)))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase));

            var subfolders = Directory.GetDirectories(folder)
                .Where(subfolder => !Path.GetFileName(subfolder).StartsWith('.'))
                .Where(subfolder => new DirectoryInfo(subfolder).LinkTarget == null)
                .OrderBy(subfolder => subfolder, StringComparer.OrdinalIgnoreCase);

            foreach (var subfolder in subfolders)
            {
                Collect(subfolder);
            }
        }
    }

    /// <returns>Whether the download may go ahead: nothing is to be deleted, or the user agreed to the deletions.</returns>
    private bool ConfirmClean(string folderPath, List<string> toDelete)
    {
        if (toDelete.Count == 0) return true;

        if (toDelete.Count > MaxCleanDeletions)
        {
            Console.Error.WriteLine(
                $"Error: --clean would delete {toDelete.Count} file(s) from '{folderPath}', over the limit of " +
                $"{MaxCleanDeletions}. Nothing was downloaded or deleted.");
            return false;
        }

        Console.WriteLine($"--clean will delete these {toDelete.Count} file(s) from '{folderPath}':");
        foreach (var file in toDelete)
        {
            Console.WriteLine($"  {Path.GetRelativePath(folderPath, file)}");
        }

        if (confirmPrompt.Confirm($"Delete these {toDelete.Count} file(s) and download?")) return true;

        Console.WriteLine("Cancelled. Nothing was downloaded or deleted.");
        return false;
    }

    /// <summary>
    /// Deletes <paramref name="files"/>, then every folder that leaves empty, up to but not including
    /// <paramref name="folderPath"/> itself.
    /// </summary>
    private static void DeleteFiles(string folderPath, List<string> files)
    {
        if (files.Count == 0) return;

        foreach (var file in files)
        {
            File.Delete(file);
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        var foldersRemoved = 0;

        var folders = files
            .Select(file => Path.GetDirectoryName(Path.GetFullPath(file))!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(folder => folder.Length);

        foreach (var start in folders)
        {
            var folder = start;
            while (!string.Equals(folder, root, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(folder)
                && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
                foldersRemoved++;
                folder = Path.GetDirectoryName(folder)!;
            }
        }

        Console.WriteLine(
            $"Deleted {files.Count} file(s) not in the download" +
            (foldersRemoved > 0 ? $", and {foldersRemoved} folder(s) left empty." : "."));
    }

    private async Task<DownloadTally> DownloadLeafAsync(LeafDownload leaf, bool overwrite)
    {
        var tally = new DownloadTally();

        foreach (var entry in leaf.Index)
        {
            var path = leaf.PathFor(entry);

            var document = await dbInterface.GetDocument(leaf.Category, entry.Filename);
            if (document == null) continue;

            var existing = await CompareWithExistingAsync(path, document.Content);
            if (existing == ExistingFile.Different && !overwrite)
            {
                Console.Error.WriteLine($"Skipped (already exists and differs): {path}");
                tally = tally with { Skipped = tally.Skipped + 1 };
                continue;
            }

            await WriteAsync(path, document.Content, existing);
            Console.WriteLine($"{entry.Filename} -> {path} {DescribeWrite(existing)}");
            tally = existing switch
            {
                ExistingFile.Missing => tally with { Created = tally.Created + 1 },
                ExistingFile.Different => tally with { Overwritten = tally.Overwritten + 1 },
                _ => tally with { Unchanged = tally.Unchanged + 1 }
            };
        }

        Console.WriteLine(
            $"Downloaded {tally.Written} document(s) from category '{leaf.Category}' to {leaf.Folder}: " +
            $"{tally.Created} new, {tally.Overwritten} overwritten, {tally.Unchanged} unchanged.");
        return tally;
    }

    private static void ReportSkipped(int skipped)
    {
        if (skipped > 0)
        {
            Console.WriteLine(
                $"Skipped {skipped} existing file(s) that differ from the stored version; pass --overwrite to replace them.");
        }
    }

    private async Task DownloadDocumentAsync(string category, string filename, string targetPath, bool overwrite)
    {
        var document = await dbInterface.GetDocument(category, filename);

        if (document == null)
        {
            Console.Error.WriteLine($"No document named '{filename}' exists in category '{category}'.");
            return;
        }

        var path = IsFolderPath(targetPath)
            ? Path.Combine(targetPath, SanitiseFileName(filename))
            : targetPath;

        var existing = await CompareWithExistingAsync(path, document.Content);
        if (!CanWrite(path, existing, overwrite)) return;

        await WriteAsync(path, document.Content, existing);
        Console.WriteLine($"{filename} -> {path} {DescribeWrite(existing)}");
    }

    private async Task DownloadBeatsAsync(string category, int sessionNumber, string path, bool overwrite)
    {
        var beats = CanonicalBeats.Select(await dbInterface.GetBeatsForSession(category, sessionNumber));

        if (beats.Count == 0)
        {
            Console.Error.WriteLine($"No beats found for session {sessionNumber} in category '{category}'.");
            return;
        }

        var separator = Environment.NewLine + Environment.NewLine;
        var content = string.Join(separator, beats.Select(b => b.Content.TrimEnd())) + Environment.NewLine;

        var existing = await CompareWithExistingAsync(path, content);
        if (!CanWrite(path, existing, overwrite)) return;

        await WriteAsync(path, content, existing);
        Console.WriteLine($"Downloaded {beats.Count} beat(s) of session {sessionNumber} to {path} {DescribeWrite(existing)}.");
    }

    /// <summary>
    /// An identical file never blocks a download, since leaving it in place is all an overwrite would do.
    /// </summary>
    private static bool CanWrite(string path, ExistingFile existing, bool overwrite)
    {
        if (existing != ExistingFile.Different || overwrite) return true;

        Console.Error.WriteLine($"Error: '{path}' already exists and differs from the stored version; pass --overwrite to replace it.");
        return false;
    }

    private static async Task<ExistingFile> CompareWithExistingAsync(string path, string content)
    {
        if (!File.Exists(path)) return ExistingFile.Missing;

        return await File.ReadAllTextAsync(path) == content ? ExistingFile.Identical : ExistingFile.Different;
    }

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/>, leaving an identical file untouched.
    /// </summary>
    private static async Task WriteAsync(string path, string content, ExistingFile existing)
    {
        if (existing == ExistingFile.Identical) return;

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, content);
    }

    private static string DescribeWrite(ExistingFile existing) => existing switch
    {
        ExistingFile.Missing => "(new)",
        ExistingFile.Different => "(overwritten)",
        _ => "(unchanged)"
    };

    private static bool IsFolderPath(string path) =>
        Directory.Exists(path)
        || path.EndsWith(Path.DirectorySeparatorChar)
        || path.EndsWith(Path.AltDirectorySeparatorChar);

    private static string SanitiseFileName(string filename)
    {
        var sanitised = string.Concat(filename.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitised) ? "untitled" : sanitised;
    }

    private sealed record LeafDownload(string Category, string Folder, List<DocumentIndexEntry> Index)
    {
        public string PathFor(DocumentIndexEntry entry) => Path.Combine(Folder, SanitiseFileName(entry.Filename));
    }

    private enum ExistingFile
    {
        Missing,
        Identical,
        Different
    }

    private readonly record struct DownloadTally(int Created, int Overwritten, int Unchanged, int Skipped)
    {
        public int Written => Created + Overwritten;

        public static DownloadTally operator +(DownloadTally left, DownloadTally right) => new(
            left.Created + right.Created,
            left.Overwritten + right.Overwritten,
            left.Unchanged + right.Unchanged,
            left.Skipped + right.Skipped);
    }
}
