using FluentAssertions;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Tests.Shared.Results;

public class ResultTests
{
    [Fact]
    public void Map_OnSuccess_ShouldMapTheValue()
    {
        var result = Result<int, string>.Succeed(3);

        var mapped = result.Map(onSuccess: value => $"value {value}", onFailure: error => $"error {error}");

        mapped.Should().Be("value 3");
    }

    [Fact]
    public void Map_OnFailure_ShouldMapTheError()
    {
        var result = Result<int, string>.Fail("broken");

        var mapped = result.Map(onSuccess: value => $"value {value}", onFailure: error => $"error {error}");

        mapped.Should().Be("error broken");
    }

    [Fact]
    public void Act_OnSuccess_ShouldRunOnlyTheSuccessBranch()
    {
        var result = Result<int, string>.Succeed(3);
        int? seenValue = null;
        string? seenError = null;

        result.Act(onSuccess: value => seenValue = value, onFailure: error => seenError = error);

        seenValue.Should().Be(3);
        seenError.Should().BeNull();
    }

    [Fact]
    public void Act_OnFailure_ShouldRunOnlyTheFailureBranch()
    {
        var result = Result<int, string>.Fail("broken");
        int? seenValue = null;
        string? seenError = null;

        result.Act(onSuccess: value => seenValue = value, onFailure: error => seenError = error);

        seenValue.Should().BeNull();
        seenError.Should().Be("broken");
    }

    [Fact]
    public async Task FailAsTask_ShouldYieldAFailure()
    {
        var result = await Result<int, string>.FailAsTask("broken");

        result.Should().BeOfType<FailureResult<int, string>>()
            .Which.Error.Should().Be("broken");
    }
}
