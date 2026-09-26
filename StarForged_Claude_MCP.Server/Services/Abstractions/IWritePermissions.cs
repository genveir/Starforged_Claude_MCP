namespace StarForged_Claude_MCP.Server.Services.Abstractions;

public interface IWritePermissions
{
    bool IsWriteEnabled(string category);

    void EnableWrite(string category);

    void DisableWrite(string category);
}
