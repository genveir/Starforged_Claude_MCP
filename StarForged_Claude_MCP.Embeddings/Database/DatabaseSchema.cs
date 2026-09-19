using System.Text.RegularExpressions;

namespace StarForged_Claude_MCP.Embeddings.Database;

/// <summary>
/// Exposes CreateScript.sql, the single source of truth for the database schema,
/// so callers do not restate the schema alongside it.
/// </summary>
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

    /// <summary>
    /// The batches of the create script that build the tables, with the batches that create
    /// and select the database itself removed. Lets a caller apply the schema to a database
    /// of its own naming — an integration test database, for instance — while still taking
    /// the table definitions from the script.
    /// </summary>
    public static IReadOnlyList<string> GetTableCreationBatches() =>
        BatchSeparator.Split(ReadCreateScript())
            .Select(batch => batch.Trim())
            .Where(batch => batch.Length > 0 && !DatabaseScopedBatch.IsMatch(batch))
            .ToList();
}
