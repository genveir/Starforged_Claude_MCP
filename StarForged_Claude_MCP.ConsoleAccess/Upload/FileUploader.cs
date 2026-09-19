using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services;
using System.Collections.Concurrent;
using System.Text;

namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public class FileUploader
{
    private readonly IDocumentProcessingService documentProcessingService;
    private readonly DbInterface dbInterface;
    private readonly BeatPreprocessor beatPreprocessor;
    private readonly ISummaryPrompt summaryPrompt;

    public FileUploader(
        IDocumentProcessingService documentProcessingService,
        DbInterface dbInterface,
        BeatPreprocessor beatPreprocessor,
        ISummaryPrompt summaryPrompt)
    {
        this.documentProcessingService = documentProcessingService;
        this.dbInterface = dbInterface;
        this.beatPreprocessor = beatPreprocessor;
        this.summaryPrompt = summaryPrompt;
    }

    public async Task UploadFile(UploadOptions options, CancellationToken cancellationToken)
    {
        if (options.Mode == UploadMode.Folder && !Directory.Exists(options.FolderPath))
        {
            Console.Error.WriteLine($"Error: Folder '{options.FolderPath}' does not exist.");
            return;
        }

        switch (options.Mode)
        {
            case UploadMode.Folder:
                await UploadFolderAsync(options.Category, options.FolderPath!, options.Indexed, options.Summaries);
                break;
            case UploadMode.Beats:
                await RunBeatsAsync(options.Category, options.SessionNumber, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Invalid upload mode {options.Mode}");
        }
    }

    private async Task UploadFolderAsync(string category, string folderPath, bool indexed, SummaryMode summaries)
    {
        var files = Directory.GetFiles(folderPath, "*.md", SearchOption.AllDirectories);

        Console.WriteLine($"Found {files.Length} file(s) to process.");

        var stored = 0;
        var replaced = 0;

        foreach (var filePath in files)
        {
            Console.WriteLine($"Processing: {filePath}");

            var text = await File.ReadAllTextAsync(filePath);
            var filename = Path.GetFileName(filePath);

            var existing = await dbInterface.GetDocument(category, filename);

            // Asked for before anything is written, so that abandoning a run part way through
            // never leaves a document stored without the summary that was being typed for it.
            var summary = ResolveSummary(filename, existing?.Summary, summaries);

            if (existing == null)
            {
                await StoreDocumentAsync(category, filename, text, summary, indexed);
                Console.WriteLine(indexed ? "  Stored and indexed." : "  Stored.");
                stored++;
            }
            else
            {
                await ReplaceDocumentAsync(existing, text, summary, indexed);
                Console.WriteLine(indexed ? "  Replaced and reindexed." : "  Replaced.");
                replaced++;
            }
        }

        Console.WriteLine($"\nCompleted! {stored} document(s) stored, {replaced} replaced.");
    }

    private async Task RunBeatsAsync(string category, int sessionNumber, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Listening on stdin. Category: {category}. Session: {sessionNumber}. Press Ctrl+C to exit.");
        Console.WriteLine(await FormatLoggedBeatsAsync(category, sessionNumber));

        await RunStdinLoopAsync(
            store: async content =>
            {
                var (beatNumber, version, beatContent) = beatPreprocessor.Process(content);
                var id = await dbInterface.StoreBeat(category, sessionNumber, beatNumber, version, beatContent);

                var label = beatNumber == null ? "unnumbered" : $"{beatNumber}.{version}";
                return (Id: id, Message: $"  Stored beat [{label}]\n{await FormatLoggedBeatsAsync(category, sessionNumber)}");
            },
            undo: async id =>
            {
                await dbInterface.DeleteBeat(id);
                return $"  Undone: removed the last beat.\n{await FormatLoggedBeatsAsync(category, sessionNumber)}";
            },
            cancellationToken);
    }

    private string? ResolveSummary(string filename, string? existingSummary, SummaryMode summaries) => summaries switch
    {
        SummaryMode.All => summaryPrompt.Ask(filename, existingSummary),
        SummaryMode.Missing => existingSummary ?? summaryPrompt.Ask(filename, existingSummary: null),
        SummaryMode.None => existingSummary,
        SummaryMode.Drop => null,
        _ => throw new ArgumentException($"Unknown summary mode {summaries}", nameof(summaries))
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

    private async Task ReplaceDocumentAsync(Document existing, string content, string? summary, bool indexed)
    {
        await dbInterface.UpdateDocument(existing.Id, content, summary);

        if (indexed)
        {
            await documentProcessingService.IndexDocumentAsync(content, existing.Id, DocumentProcessorToUse.Markdown);
        }
        else
        {
            await documentProcessingService.RemoveIndexForDocumentAsync(existing.Id);
        }
    }

    private async Task<string> FormatLoggedBeatsAsync(string category, int sessionNumber)
    {
        var beats = await dbInterface.GetBeatsForSession(category, sessionNumber);
        var display = string.Join(", ", beats.Select(b => b.BeatNumber == null ? "None" : $"{b.BeatNumber}.{b.Version}"));
        return $"Currently logged beats: [{display}]";
    }

    private static async Task RunStdinLoopAsync(
        Func<string, Task<(int Id, string Message)>> store,
        Func<int, Task<string>> undo,
        CancellationToken cancellationToken)
    {
        const string UndoSentinel = "\x1A";

        var lines = new ConcurrentQueue<string>();
        var dataAvailable = new SemaphoreSlim(0);

        var readerThread = new Thread(() =>
        {
            if (Console.IsInputRedirected)
            {
                string? line;
                while ((line = Console.ReadLine()) != null)
                {
                    lines.Enqueue(line);
                    dataAvailable.Release();
                }
            }
            else
            {
                var lineBuilder = new StringBuilder();
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.KeyChar is '\n' or '\r' || key.Key == ConsoleKey.Enter)
                        {
                            lines.Enqueue(lineBuilder.ToString());
                            lineBuilder.Clear();
                            dataAvailable.Release();
                        }
                        else if (key.Key == ConsoleKey.Z && (key.Modifiers & ConsoleModifiers.Control) != 0)
                        {
                            lines.Enqueue(UndoSentinel);
                            dataAvailable.Release();
                        }
                        else if (key.KeyChar != '\0')
                        {
                            lineBuilder.Append(key.KeyChar);
                        }
                    }
                    else
                    {
                        if (lineBuilder.Length > 0)
                        {
                            lines.Enqueue(lineBuilder.ToString());
                            lineBuilder.Clear();
                            dataAvailable.Release();
                        }
                        Thread.Sleep(10);
                    }
                }
            }
        })
        { IsBackground = true };

        readerThread.Start();

        var undoStack = new Stack<int>();
        var buffer = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int timeout = buffer.Length > 0 ? 100 : Timeout.Infinite;
                bool gotSignal = await dataAvailable.WaitAsync(timeout, cancellationToken);

                if (gotSignal)
                {
                    while (lines.TryDequeue(out var line))
                    {
                        if (line == UndoSentinel)
                        {
                            if (undoStack.Count == 0)
                            {
                                Console.WriteLine("  Nothing to undo.");
                                continue;
                            }
                            Console.WriteLine(await undo(undoStack.Pop()));
                        }
                        else
                        {
                            buffer.AppendLine(line);
                        }
                    }
                }
                else if (buffer.Length > 0)
                {
                    var content = buffer.ToString();
                    buffer.Clear();

                    var (id, message) = await store(content);
                    undoStack.Push(id);
                    Console.WriteLine(message);
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
