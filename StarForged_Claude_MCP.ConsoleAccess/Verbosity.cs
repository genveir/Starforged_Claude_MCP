namespace StarForged_Claude_MCP.ConsoleAccess;

/// <summary>How much an upload or download reports about each file.</summary>
public enum Verbosity
{
    /// <summary>Report every file, including those left unchanged.</summary>
    All,

    /// <summary>Report only the files that were, or would be, changed; totals are always reported.</summary>
    Changed
}
