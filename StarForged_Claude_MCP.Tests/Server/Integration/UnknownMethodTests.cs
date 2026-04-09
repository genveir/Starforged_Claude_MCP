using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class UnknownMethodTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    [Fact]
    public async Task UnknownMethod_ShouldReturnMethodNotFound()
    {
        var request = new JsonRpcRequest
        {
            Id = "6",
            Method = "unknown/method",
            Params = new { }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("6");
        response.Error.Should().NotBeNull();
        response.Error.Code.Should().Be(-32601);
        response.Error.Message.Should().Contain("Method not found");
    }
}
