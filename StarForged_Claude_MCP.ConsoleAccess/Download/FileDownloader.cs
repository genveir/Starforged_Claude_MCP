using StarForged_Claude_MCP.Embeddings.Database;

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
        var document = await dbInterface.GetDocument(options.Category, options.Filename);

        if (document == null)
        {
            Console.Error.WriteLine($"No document named '{options.Filename}' exists in category '{options.Category}'.");
            return;
        }

        Console.WriteLine(document.Content);
    }
}
