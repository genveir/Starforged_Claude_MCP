using System.Text.RegularExpressions;

namespace StarForged_Claude_MCP.Embeddings.Database;

public static class DatabaseSchema
{
    private const string ResourceName = "StarForged_Claude_MCP.Embeddings.Database.CreateScript.sql";

    private static readonly Regex BatchSeparator =
        new(@"^\s*go\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex DatabaseScopedBatch =
        new(@"^\s*(create\s+database|use)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ReadCreateScript()
    {
        using var stream = typeof(DatabaseSchema).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<string> GetTableCreationBatches() =>
        BatchSeparator.Split(ReadCreateScript())
            .Select(batch => batch.Trim())
            .Where(batch => batch.Length > 0 && !DatabaseScopedBatch.IsMatch(batch))
            .ToList();
}
