using StarForged_Claude_MCP.Embeddings.Database;
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
        var index = await dbInterface.GetDocumentIndex(category);

        if (index.Count == 0)
        {
            Console.Error.WriteLine($"No documents found for category '{category}'.");
            return;
        }

        int written = 0;
        int skipped = 0;

        foreach (var entry in index)
        {
            var path = Path.Combine(folderPath, SanitiseFileName(entry.Filename));

            if (File.Exists(path) && !overwrite)
            {
                Console.Error.WriteLine($"Skipped (already exists): {path}");
                skipped++;
                continue;
            }

            var document = await dbInterface.GetDocument(category, entry.Filename);
            if (document == null) continue;

            await WriteAsync(path, document.Content);
            Console.WriteLine($"{entry.Filename} -> {path}");
            written++;
        }

        Console.WriteLine($"Downloaded {written} document(s) from category '{category}' to {folderPath}.");
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

        await WriteAsync(path, document.Content);
        Console.WriteLine($"{filename} -> {path}");
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

        await WriteAsync(path, content);
        Console.WriteLine($"Downloaded {beats.Count} beat(s) of session {sessionNumber} to {path}.");
    }

    private static bool CanWrite(string path, bool overwrite)
    {
        if (!File.Exists(path) || overwrite) return true;

        Console.Error.WriteLine($"Error: '{path}' already exists; pass --overwrite to replace it.");
        return false;
    }

    private static async Task WriteAsync(string path, string content)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, content);
    }

    private static bool IsFolderPath(string path) =>
        Directory.Exists(path)
        || path.EndsWith(Path.DirectorySeparatorChar)
        || path.EndsWith(Path.AltDirectorySeparatorChar);

    private static string SanitiseFileName(string filename)
    {
        var sanitised = string.Concat(filename.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitised) ? "untitled" : sanitised;
    }
}
