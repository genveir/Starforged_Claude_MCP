using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Server.Services.Abstractions;

public interface IWritePermissions
{
    bool IsWriteEnabled(Category category);

    void EnableWrite(Category category);

    void DisableWrite(Category category);
}
