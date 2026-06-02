using FluentAssertions;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class GetCanonicalBeatsTests(TestFixture fixture) : DocumentsTestBase(fixture)
{
    [Fact]
    public async Task GetCanonicalBeats_ShouldReturnBeatsForSession()
    {
        await ClearTestDocuments();

        await AddTestDocument("Prologue: the party received a mysterious invitation.", "SessionBeats_5", null);
        await AddTestDocument("The party arrived in the city of Ironhaven.", "SessionBeats_5", "1.0");
        await AddTestDocument("They discovered the merchant was a spy.", "SessionBeats_5", "2.0");
        await AddTestDocument("They discovered the butcher was a spy.", "SessionBeats_5", "2.1");
        await AddTestDocument("Meanwhile, the rival adventuring party plotted in the shadows.", "SessionBeats_5", null);
        await AddTestDocument("A chase through the market ended with an arrest.", "SessionBeats_5", "3.0");

        var request = new JsonRpcRequest
        {
            Id = "11",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "get_canonical_beats",
                Arguments = new Dictionary<string, object>
                {
                    { "sessionNumber", 5 }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("11");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var documents = toolResponse.GetProperty("documents").EnumerateArray().ToArray();

        documents.Should().HaveCount(5);
        checkDocument(documents[0], "Prologue: the party received a mysterious invitation.", 1, null);
        checkDocument(documents[1], "The party arrived in the city of Ironhaven.", 2, "1.0");
        checkDocument(documents[2], "They discovered the butcher was a spy.", 3, "2.1");
        checkDocument(documents[3], "Meanwhile, the rival adventuring party plotted in the shadows.", 4, null);
        checkDocument(documents[4], "A chase through the market ended with an arrest.", 5, "3.0");

        void checkDocument(JsonElement doc, string expectedContent, int expectedSequence, string? expectedBeatNumber)
        {
            doc.GetProperty("content").GetString().Should().Be(expectedContent);
            doc.GetProperty("sequence").GetInt32().Should().Be(expectedSequence);
            if (expectedBeatNumber == null)
                doc.TryGetProperty("beatNumber", out _).Should().BeFalse();
            else
                doc.GetProperty("beatNumber").GetString().Should().Be(expectedBeatNumber);
        }
    }

    [Fact]
    public async Task GetCanonicalBeats_WithCategoryFilter_ShouldReturnOnlyMatchingBeats()
    {
        await ClearTestDocuments();

        await AddTestDocument("Prologue: the party received a mysterious invitation.", "SessionBeats_6", null, category: null);
        await AddTestDocument("The party arrived in the city of Ironhaven.", "SessionBeats_6", "1.0", category: "exploration");
        await AddTestDocument("They discovered the merchant was a spy.", "SessionBeats_6", "2.0", category: "intrigue");
        await AddTestDocument("They discovered the butcher was a spy.", "SessionBeats_6", "2.1", category: "intrigue");
        await AddTestDocument("A chase through the market ended with an arrest.", "SessionBeats_6", "3.0", category: "exploration");

        var request = new JsonRpcRequest
        {
            Id = "14",
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = "get_canonical_beats",
                Arguments = new Dictionary<string, object>
                {
                    { "sessionNumber", 6 },
                    { "category", "intrigue" }
                }
            }
        };

        var response = await InvokeServerMethod(request);

        response.Should().NotBeNull();
        response.Id.Should().Be("14");
        response.Error.Should().BeNull();

        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions),
            _jsonOptions);

        var toolResponse = JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
        var documents = toolResponse.GetProperty("documents").EnumerateArray().ToArray();

        documents.Should().HaveCount(1);
        documents[0].GetProperty("content").GetString().Should().Be("They discovered the butcher was a spy.");
        documents[0].GetProperty("beatNumber").GetString().Should().Be("2.1");
        documents[0].GetProperty("sequence").GetInt32().Should().Be(1);
    }
}
