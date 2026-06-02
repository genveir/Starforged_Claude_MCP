using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public abstract class DocumentsTestBase : McpServerTestBase
{
    protected DocumentsTestBase(TestFixture fixture) : base(fixture) { }

    protected async Task ClearTestDocuments()
    {
        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        await dbInterface.DeleteAllDocuments();
    }

    protected async Task AddTestDocument(string text, string sourceDocument, string? beatNumber = null, string? summary = null, string? category = null)
    {
        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        await dbInterface.StoreDocument(text, sourceDocument, beatNumber, summary, category);
    }
}
