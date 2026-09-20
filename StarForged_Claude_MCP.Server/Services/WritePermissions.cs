namespace StarForged_Claude_MCP.Server.Services;

public class WritePermissions : IWritePermissions
{
    private readonly HashSet<string> _writeEnabledCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public bool IsWriteEnabled(string category)
    {
        lock (_lock)
        {
            return _writeEnabledCategories.Contains(category);
        }
    }

    public void EnableWrite(string category)
    {
        lock (_lock)
        {
            _writeEnabledCategories.Add(category);
        }
    }

    public void DisableWrite(string category)
    {
        lock (_lock)
        {
            _writeEnabledCategories.Remove(category);
        }
    }
}
