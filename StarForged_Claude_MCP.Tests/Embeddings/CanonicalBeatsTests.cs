using FluentAssertions;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services;

namespace StarForged_Claude_MCP.Tests.Embeddings;

public class CanonicalBeatsTests
{
    private static Beat Numbered(int id, int beatNumber, int version, string content) =>
        new() { Id = id, BeatNumber = beatNumber, Version = version, Content = content };

    private static Beat Unnumbered(int id, string content) =>
        new() { Id = id, Content = content };

    [Fact]
    public void Select_WithNoSupersessions_ShouldKeepWriteOrder()
    {
        var beats = CanonicalBeats.Select([
            Unnumbered(1, "Prologue"),
            Numbered(2, beatNumber: 1, version: 0, "One"),
            Numbered(3, beatNumber: 2, version: 0, "Two"),
            Unnumbered(4, "Interlude"),
        ]);

        beats.Select(b => b.Content).Should().Equal("Prologue", "One", "Two", "Interlude");
        beats.Select(b => b.Sequence).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Select_ShouldPlaceACorrectionWhereTheOriginalSat()
    {
        var beats = CanonicalBeats.Select([
            Numbered(1, beatNumber: 1, version: 0, "One"),
            Numbered(2, beatNumber: 2, version: 0, "Two, original"),
            Unnumbered(3, "Interlude"),
            Numbered(4, beatNumber: 3, version: 0, "Three"),
            Numbered(5, beatNumber: 2, version: 1, "Two, corrected"),
        ]);

        beats.Select(b => b.Content).Should().Equal("One", "Two, corrected", "Interlude", "Three");
    }

    [Fact]
    public void Select_WithSeveralCorrections_ShouldKeepOnlyTheHighestVersion()
    {
        var beats = CanonicalBeats.Select([
            Numbered(1, beatNumber: 1, version: 0, "One, original"),
            Numbered(2, beatNumber: 1, version: 1, "One, second try"),
            Numbered(3, beatNumber: 1, version: 2, "One, third try"),
        ]);

        beats.Should().ContainSingle();
        beats[0].Content.Should().Be("One, third try");
        beats[0].Version.Should().Be(2);
    }

    [Fact]
    public void Select_WhenABeatStartsAboveVersionZero_ShouldAnchorOnItsEarliestVersion()
    {
        var beats = CanonicalBeats.Select([
            Numbered(1, beatNumber: 1, version: 0, "One"),
            Numbered(2, beatNumber: 2, version: 1, "Two, first written as version one"),
            Numbered(3, beatNumber: 3, version: 0, "Three"),
            Numbered(4, beatNumber: 2, version: 2, "Two, corrected"),
        ]);

        beats.Select(b => b.Content).Should().Equal(
            "One",
            "Two, corrected",
            "Three");
    }

    [Fact]
    public void Select_ShouldNotCollapseUnnumberedBeatsTogether()
    {
        var beats = CanonicalBeats.Select([
            Unnumbered(1, "First vignette"),
            Unnumbered(2, "Second vignette"),
            Unnumbered(3, "Third vignette"),
        ]);

        beats.Should().HaveCount(3, because: "unnumbered beats share no identity and never supersede each other");
    }

    [Fact]
    public void Select_WithNothingWritten_ShouldReturnEmpty()
    {
        CanonicalBeats.Select([]).Should().BeEmpty();
    }
}
