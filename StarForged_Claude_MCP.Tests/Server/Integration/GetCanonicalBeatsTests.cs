using FluentAssertions;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class GetCanonicalBeatsTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Category = "session_log";

    [Fact]
    public async Task GetCanonicalBeats_ShouldReturnLatestVersionsInNarrativeOrder()
    {
        await ClearTestBeats();

        await Db.StoreBeat(Category, sessionNumber: 5, beatNumber: null, version: null, content: "Prologue: a mysterious invitation.");
        await Db.StoreBeat(Category, sessionNumber: 5, beatNumber: 1, version: 0, content: "The party arrived in Ironhaven.");
        await Db.StoreBeat(Category, sessionNumber: 5, beatNumber: 2, version: 0, content: "The merchant was a spy.");
        await Db.StoreBeat(Category, sessionNumber: 5, beatNumber: null, version: null, content: "Interlude: rivals plotted in the shadows.");
        await Db.StoreBeat(Category, sessionNumber: 5, beatNumber: 3, version: 0, content: "A chase ended with an arrest.");

        var beats = await GetBeats("1", sessionNumber: 5);

        beats.Select(b => b.GetProperty("content").GetString()).Should().Equal(
            "Prologue: a mysterious invitation.",
            "The party arrived in Ironhaven.",
            "The merchant was a spy.",
            "Interlude: rivals plotted in the shadows.",
            "A chase ended with an arrest.");

        beats.Select(b => b.GetProperty("sequence").GetInt32()).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task GetCanonicalBeats_WhenABeatIsCorrectedLater_ShouldKeepItsOriginalPlace()
    {
        await ClearTestBeats();

        await Db.StoreBeat(Category, sessionNumber: 6, beatNumber: 1, version: 0, content: "Beat one.");
        await Db.StoreBeat(Category, sessionNumber: 6, beatNumber: 2, version: 0, content: "Beat two, as first written.");
        await Db.StoreBeat(Category, sessionNumber: 6, beatNumber: null, version: null, content: "An interlude between two and three.");
        await Db.StoreBeat(Category, sessionNumber: 6, beatNumber: 3, version: 0, content: "Beat three.");
        await Db.StoreBeat(Category, sessionNumber: 6, beatNumber: 2, version: 1, content: "Beat two, corrected much later.");

        var beats = await GetBeats("2", sessionNumber: 6);

        beats.Select(b => b.GetProperty("content").GetString()).Should().Equal(
            "Beat one.",
            "Beat two, corrected much later.",
            "An interlude between two and three.",
            "Beat three.");

        beats.Should().HaveCount(4, because: "the superseded version of beat two is not returned");
    }

    [Fact]
    public async Task GetCanonicalBeats_ShouldOnlyReturnTheRequestedSessionAndCategory()
    {
        await ClearTestBeats();

        await Db.StoreBeat(Category, sessionNumber: 7, beatNumber: 1, version: 0, content: "Session seven, this category.");
        await Db.StoreBeat(Category, sessionNumber: 8, beatNumber: 1, version: 0, content: "Session eight, this category.");
        await Db.StoreBeat("another_campaign", sessionNumber: 7, beatNumber: 1, version: 0, content: "Session seven, another category.");

        var beats = await GetBeats("3", sessionNumber: 7);

        beats.Should().HaveCount(1);
        beats[0].GetProperty("content").GetString().Should().Be("Session seven, this category.");
    }

    [Fact]
    public async Task GetCanonicalBeats_ForAnUnknownSession_ShouldReturnEmpty()
    {
        await ClearTestBeats();

        var beats = await GetBeats("4", sessionNumber: 999);

        beats.Should().BeEmpty();
    }

    private async Task<JsonElement[]> GetBeats(string id, int sessionNumber)
    {
        var response = await CallTool(id, "get_canonical_beats", new Dictionary<string, object>
        {
            ["category"] = Category,
            ["sessionNumber"] = sessionNumber
        });

        response.ShouldHaveSucceeded();
        return ToolPayload(response).GetProperty("beats").EnumerateArray().ToArray();
    }
}
