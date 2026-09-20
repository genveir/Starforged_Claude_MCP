namespace StarForged_Claude_MCP.Server.Services;

public interface IWritePermissions
{
    bool IsWriteEnabled(string category);

    void EnableWrite(string category);

    void DisableWrite(string category);
}
