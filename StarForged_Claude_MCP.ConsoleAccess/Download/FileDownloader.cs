using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services;

namespace StarForged_Claude_MCP.ConsoleAccess.Download;

public class FileDownloader
{
    private readonly DbInterface dbInterface;

    public FileDownloader(DbInterface dbInterface)
    {
        this.dbInterface = dbInterface;
    }

    public async Task DownloadFile(DownloadOptions options)
    {
        switch (options.Mode)
        {
            case DownloadMode.Folder:
                await DownloadFolderAsync(options.Category, options.TargetPath, options.Overwrite);
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

    private async Task DownloadFolderAsync(string category, string folderPath, bool overwrite)
    {
        var leaves = await dbInterface.GetCategoriesUnder(category);
        if (leaves.Count > 0)
        {
            await DownloadParentAsync(category, leaves, folderPath, overwrite);
            return;
        }

        var index = await dbInterface.GetDocumentIndex(category);
        if (index.Count == 0)
        {
            Console.Error.WriteLine($"No documents found for category '{category}'.");
            return;
        }

        var tally = await DownloadLeafAsync(category, index, folderPath, overwrite);
        ReportSkipped(tally.Skipped);
    }

    /// <summary>
    /// Downloads every leaf under <paramref name="category"/> into its own folder, nested to mirror the category
    /// tree: leaf 'Campaign.Npcs.Allies' under 'Campaign' goes to 'Npcs/Allies' inside <paramref name="folderPath"/>.
    /// </summary>
    private async Task DownloadParentAsync(string category, List<string> leaves, string folderPath, bool overwrite)
    {
        var total = new DownloadTally();

        foreach (var leaf in leaves)
        {
            var relativeSegments = leaf[(category.Length + 1)..].Split(CategoryPath.Separator).Select(SanitiseFileName);
            var leafFolder = Path.Combine([folderPath, .. relativeSegments]);

            var index = await dbInterface.GetDocumentIndex(leaf);
            total += await DownloadLeafAsync(leaf, index, leafFolder, overwrite);
        }

        Console.WriteLine(
            $"Downloaded {total.Written} document(s) from {leaves.Count} categor{(leaves.Count == 1 ? "y" : "ies")} " +
            $"under '{category}' to {folderPath}: {total.Created} new, {total.Overwritten} overwritten.");
        ReportSkipped(total.Skipped);
    }

    private async Task<DownloadTally> DownloadLeafAsync(
        string category, List<DocumentIndexEntry> index, string folderPath, bool overwrite)
    {
        var tally = new DownloadTally();

        foreach (var entry in index)
        {
            var path = Path.Combine(folderPath, SanitiseFileName(entry.Filename));

            if (File.Exists(path) && !overwrite)
            {
                Console.Error.WriteLine($"Skipped (already exists): {path}");
                tally = tally with { Skipped = tally.Skipped + 1 };
                continue;
            }

            var document = await dbInterface.GetDocument(category, entry.Filename);
            if (document == null) continue;

            var replacedExisting = await WriteAsync(path, document.Content);
            Console.WriteLine($"{entry.Filename} -> {path} {DescribeWrite(replacedExisting)}");
            tally = replacedExisting
                ? tally with { Overwritten = tally.Overwritten + 1 }
                : tally with { Created = tally.Created + 1 };
        }

        Console.WriteLine(
            $"Downloaded {tally.Written} document(s) from category '{category}' to {folderPath}: " +
            $"{tally.Created} new, {tally.Overwritten} overwritten.");
        return tally;
    }

    private static void ReportSkipped(int skipped)
    {
        if (skipped > 0)
        {
            Console.WriteLine($"Skipped {skipped} existing file(s); pass --overwrite to replace them.");
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

        if (!CanWrite(path, overwrite)) return;

        var replacedExisting = await WriteAsync(path, document.Content);
        Console.WriteLine($"{filename} -> {path} {DescribeWrite(replacedExisting)}");
    }

    private async Task DownloadBeatsAsync(string category, int sessionNumber, string path, bool overwrite)
    {
        var beats = CanonicalBeats.Select(await dbInterface.GetBeatsForSession(category, sessionNumber));

        if (beats.Count == 0)
        {
            Console.Error.WriteLine($"No beats found for session {sessionNumber} in category '{category}'.");
            return;
        }

        if (!CanWrite(path, overwrite)) return;

        var separator = Environment.NewLine + Environment.NewLine;
        var content = string.Join(separator, beats.Select(b => b.Content.TrimEnd())) + Environment.NewLine;

        var replacedExisting = await WriteAsync(path, content);
        Console.WriteLine($"Downloaded {beats.Count} beat(s) of session {sessionNumber} to {path} {DescribeWrite(replacedExisting)}.");
    }

    private static bool CanWrite(string path, bool overwrite)
    {
        if (!File.Exists(path) || overwrite) return true;

        Console.Error.WriteLine($"Error: '{path}' already exists; pass --overwrite to replace it.");
        return false;
    }

    /// <returns>Whether an existing file was replaced, as opposed to a new one being created.</returns>
    private static async Task<bool> WriteAsync(string path, string content)
    {
        var replacedExisting = File.Exists(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, content);
        return replacedExisting;
    }

    private static string DescribeWrite(bool replacedExisting) => replacedExisting ? "(overwritten)" : "(new)";

    private static bool IsFolderPath(string path) =>
        Directory.Exists(path)
        || path.EndsWith(Path.DirectorySeparatorChar)
        || path.EndsWith(Path.AltDirectorySeparatorChar);

    private static string SanitiseFileName(string filename)
    {
        var sanitised = string.Concat(filename.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitised) ? "untitled" : sanitised;
    }

    private readonly record struct DownloadTally(int Created, int Overwritten, int Skipped)
    {
        public int Written => Created + Overwritten;

        public static DownloadTally operator +(DownloadTally left, DownloadTally right) => new(
            left.Created + right.Created,
            left.Overwritten + right.Overwritten,
            left.Skipped + right.Skipped);
    }
}
