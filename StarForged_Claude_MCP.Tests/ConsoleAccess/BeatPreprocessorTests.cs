using FluentAssertions;
using StarForged_Claude_MCP.ConsoleAccess.Upload;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class BeatPreprocessorTests
{
    private readonly BeatPreprocessor _preprocessor = new();

    [Fact]
    public void Process_WithABeatMarker_ShouldSplitNumberAndVersion()
    {
        var (beatNumber, version, content) = _preprocessor.Process(
            "Beat 2.1\n\nThe butcher was the spy all along.");

        beatNumber.Should().Be(2);
        version.Should().Be(1);
        content.Should().Be("Beat 2.1\n\nThe butcher was the spy all along.",
            because: "the marker is left in the stored text, not stripped out");
    }

    [Fact]
    public void Process_WithMultiDigitNumbers_ShouldParseBothParts()
    {
        var (beatNumber, version, _) = _preprocessor.Process("Beat 12.10\n\nMuch later in the session.");

        beatNumber.Should().Be(12);
        version.Should().Be(10);
    }

    [Fact]
    public void Process_WithNoMarker_ShouldReturnAnUnnumberedBeat()
    {
        var (beatNumber, version, content) = _preprocessor.Process(
            "The rain kept falling over the settlement, and nobody spoke.");

        beatNumber.Should().BeNull();
        version.Should().BeNull();
        content.Should().Be("The rain kept falling over the settlement, and nobody spoke.");
    }

    [Fact]
    public void Process_WhenTheOnlyMarkerIsOnTheLastLine_ShouldReturnAnUnnumberedBeat()
    {
        var (beatNumber, version, _) = _preprocessor.Process(
            "An opening vignette, with no beat of its own.\n\nBeat 3.0");

        beatNumber.Should().BeNull(because: "a trailing marker names the beat that comes next, not this one");
        version.Should().BeNull();
    }

    [Fact]
    public void Process_WithATrailingMarkerAfterItsOwn_ShouldUseTheFirst()
    {
        var (beatNumber, version, _) = _preprocessor.Process(
            "Beat 3.0\n\nThe chase ended with an arrest.\n\nNext up: Beat 4.0");

        beatNumber.Should().Be(3);
        version.Should().Be(0);
    }

    [Fact]
    public void Process_WithTrailingWhitespaceAfterTheMarker_ShouldStillTreatItAsTrailing()
    {
        var (beatNumber, _, _) = _preprocessor.Process(
            "An interlude.\n\nBeat 3.0\n\n   \n");

        beatNumber.Should().BeNull(because: "blank lines after the marker do not make it any less trailing");
    }
}
