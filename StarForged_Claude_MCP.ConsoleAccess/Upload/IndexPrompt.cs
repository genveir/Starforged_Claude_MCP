namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public interface IIndexPrompt
{
    /// <summary>
    /// Asks whether a document should be indexed. Blank input keeps <paramref name="currentlyIndexed"/>,
    /// which is null for a document that is new; a new document is left unindexed on blank input.
    /// </summary>
    bool Ask(string filename, bool? currentlyIndexed);
}

public class ConsoleIndexPrompt : IIndexPrompt
{
    public bool Ask(string filename, bool? currentlyIndexed)
    {
        var fallback = currentlyIndexed ?? false;

        while (true)
        {
            Console.Write(currentlyIndexed switch
            {
                null => $"  Index '{filename}'? [y/N]: ",
                true => $"  Index '{filename}'? It is indexed now [Y/n]: ",
                false => $"  Index '{filename}'? It is not indexed now [y/N]: "
            });

            var entered = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(entered)) return fallback;

            if (string.Equals(entered, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entered, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(entered, "n", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entered, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Console.Error.WriteLine("  Answer y or n, or leave it blank.");
        }
    }
}
