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
        var documents = await dbInterface.GetAllDocumentsForSourceDocument(options.SourceDocument);

        foreach (var document in documents)
        {
            Console.WriteLine(document.Content);
        }
    }
}
