using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services;
using System.Collections.Concurrent;
using System.Text;

namespace StarForged_Claude_MCP.DirectUpload;

public class FileUploader
{
    private readonly IDocumentProcessingService documentProcessingService;
    private readonly DbInterface dbInterface;
    private readonly BeatPreprocessor beatPreprocessor;

    public FileUploader(
        IDocumentProcessingService documentProcessingService,
        DbInterface dbInterface,
        BeatPreprocessor beatPreprocessor)
    {
        this.documentProcessingService = documentProcessingService;
        this.dbInterface = dbInterface;
        this.beatPreprocessor = beatPreprocessor;
    }

    public async Task UploadFolderAsync(string folderPath, SinkType sink, bool beatLogging)
    {
        var files = Directory.GetFiles(folderPath, "*.md", SearchOption.AllDirectories);

        Console.WriteLine($"Found {files.Length} file(s) to process.");

        var totalCount = 0;

        foreach (var filePath in files)
        {
            Console.WriteLine($"Processing: {filePath}");

            var text = await File.ReadAllTextAsync(filePath);
            var fileName = Path.GetFileName(filePath);

            var result = await RouteToSinkAsync(text, fileName, sink, beatLogging);

            Console.WriteLine(FormatResult(result, sink));
            totalCount += result.Count;
        }

        Console.WriteLine($"\nCompleted! Total items uploaded: {totalCount}");
    }

    public async Task RunContinuousAsync(string sourceDocument, SinkType sink, bool beatLogging, CancellationToken cancellationToken)
    {
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

        Console.WriteLine($"Listening on stdin. Source: {sourceDocument}. Press Ctrl+C to exit.");

        if (beatLogging)
        {
            var beats = await dbInterface.GetBeats(sourceDocument);
            var beatDisplay = string.Join(", ", beats.Select(b => b ?? "None"));
            Console.WriteLine($"Currently logged beats: [{beatDisplay}]");
        }

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
                        buffer.AppendLine(line);
                }
                else if (buffer.Length > 0)
                {
                    var content = buffer.ToString();
                    buffer.Clear();
                    var result = await RouteToSinkAsync(content, sourceDocument, sink, beatLogging);
                    Console.WriteLine(FormatResult(result, sink));
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task<UploadResult> RouteToSinkAsync(string content, string sourceDocument, SinkType sink, bool beatLogging)
    {
        string? beatNumber = null;
        if (beatLogging)
        {
            (beatNumber, content) = beatPreprocessor.Process(content);
        }

        if (sink == SinkType.Embedded)
        {
            var ids = await documentProcessingService.ProcessAndStoreDocumentAsync(content, sourceDocument, DocumentProcessorToUse.Markdown);
            return new UploadResult(ids.Length, ids);
        }
        else if (sink == SinkType.Document)
        {
            await dbInterface.StoreDocument(content, sourceDocument, beatNumber);
            return new UploadResult(1, [], beatNumber);
        }
        throw new ArgumentException("Invalid sink type.", nameof(sink));
    }

    private static string FormatResult(UploadResult result, SinkType sink) => sink switch
    {
        SinkType.Embedded => $"  Uploaded {result.Count} chunk(s) with IDs: {string.Join(", ", result.Ids)}",
        SinkType.Document when result.BeatNumber != null => $"  Stored Document [{result.BeatNumber}]",
        _ => "  Stored document."
    };

    private record UploadResult(int Count, int[] Ids, string? BeatNumber = null);
}
