using FluentAssertions;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Tests.Shared.Results;

public class ErrorResultTests
{
    [Fact]
    public void Map_OnSuccess_ShouldTakeTheSuccessBranch()
    {
        var result = ErrorResult<string>.Succeed();

        var mapped = result.Map(onSuccess: () => "success", onFailure: error => $"error {error}");

        mapped.Should().Be("success");
    }

    [Fact]
    public void Map_OnFailure_ShouldMapTheError()
    {
        var result = ErrorResult<string>.Fail("broken");

        var mapped = result.Map(onSuccess: () => "success", onFailure: error => $"error {error}");

        mapped.Should().Be("error broken");
    }

    [Fact]
    public void Act_OnSuccess_ShouldRunOnlyTheSuccessBranch()
    {
        var result = ErrorResult<string>.Succeed();
        var succeeded = false;
        string? seenError = null;

        result.Act(onSuccess: () => succeeded = true, onFailure: error => seenError = error);

        succeeded.Should().BeTrue();
        seenError.Should().BeNull();
    }

    [Fact]
    public void Act_OnFailure_ShouldRunOnlyTheFailureBranch()
    {
        var result = ErrorResult<string>.Fail("broken");
        var succeeded = false;
        string? seenError = null;

        result.Act(onSuccess: () => succeeded = true, onFailure: error => seenError = error);

        succeeded.Should().BeFalse();
        seenError.Should().Be("broken");
    }

    [Fact]
    public async Task FailAsTask_ShouldYieldAFailure()
    {
        var result = await ErrorResult<string>.FailAsTask("broken");

        result.Should().BeOfType<ErrorFailureResult<string>>()
            .Which.Error.Should().Be("broken");
    }
}
