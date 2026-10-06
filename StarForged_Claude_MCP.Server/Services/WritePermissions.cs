using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Server.Services;

public class WritePermissions : IWritePermissions
{
    private readonly HashSet<Category> _writeEnabledCategories = new();
    private readonly object _lock = new();

    public bool IsWriteEnabled(Category category)
    {
        lock (_lock)
        {
            return _writeEnabledCategories.Contains(category);
        }
    }

    public void EnableWrite(Category category)
    {
        lock (_lock)
        {
            _writeEnabledCategories.Add(category);
        }
    }

    public void DisableWrite(Category category)
    {
        lock (_lock)
        {
            _writeEnabledCategories.Remove(category);
        }
    }
}
