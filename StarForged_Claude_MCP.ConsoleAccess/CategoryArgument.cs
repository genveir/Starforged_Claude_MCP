using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.ConsoleAccess;

internal static class CategoryArgument
{
    public static bool TryTake(string[] args, out string category, out string[] rest)
    {
        category = string.Empty;
        rest = [];

        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]) || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Error: <category> is required as the first argument.");
            return false;
        }

        if (!CategoryPath.IsWellFormed(args[0]))
        {
            Console.Error.WriteLine(
                $"Error: '{args[0]}' is not a well-formed category: levels are separated by '{CategoryPath.Separator}' and none can be empty.");
            return false;
        }

        category = args[0];
        rest = args[1..];
        return true;
    }
}
