namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public interface ISummaryPrompt
{
    /// <summary>
    /// Asks for a document's summary. Returns what should be stored: blank input keeps
    /// <paramref name="existingSummary"/>, which is null for a document that is new.
    /// </summary>
    string? Ask(string filename, string? existingSummary);
}

public class ConsoleSummaryPrompt : ISummaryPrompt
{
    public const int MaxLength = 512;

    public string? Ask(string filename, string? existingSummary)
    {
        if (existingSummary != null)
        {
            Console.WriteLine($"  Current summary: {existingSummary}");
        }

        while (true)
        {
            Console.Write(existingSummary == null
                ? $"  Summary for '{filename}' (blank for none): "
                : $"  Summary for '{filename}' (blank to keep the current one): ");

            var entered = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(entered)) return existingSummary;

            entered = entered.Trim();

            if (entered.Length > MaxLength)
            {
                Console.Error.WriteLine($"  A summary cannot be over {MaxLength} characters; that one was {entered.Length}.");
                continue;
            }

            return entered;
        }
    }
}
