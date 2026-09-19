namespace StarForged_Claude_MCP.ConsoleAccess;

/// <summary>
/// Every command is scoped to a category, which is the first argument after the command
/// name — the namespace the rest of the arguments are read within.
/// </summary>
internal static class CategoryArgument
{
    public static bool TryTake(string[] args, out string category, out string[] rest)
    {
        category = string.Empty;
        rest = [];

        // A leading flag means the category was left out rather than given as "--something".
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]) || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Error: <category> is required as the first argument.");
            return false;
        }

        category = args[0];
        rest = args[1..];
        return true;
    }
}
