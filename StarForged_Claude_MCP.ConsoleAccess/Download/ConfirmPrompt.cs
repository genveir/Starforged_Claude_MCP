namespace StarForged_Claude_MCP.ConsoleAccess.Download;

public interface IConfirmPrompt
{
    /// <summary>
    /// Asks a yes/no <paramref name="question"/>. Anything but an explicit yes counts as no.
    /// </summary>
    bool Confirm(string question);
}

public class ConsoleConfirmPrompt : IConfirmPrompt
{
    public bool Confirm(string question)
    {
        Console.Write($"{question} [y/N]: ");
        var answer = Console.ReadLine()?.Trim();

        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
