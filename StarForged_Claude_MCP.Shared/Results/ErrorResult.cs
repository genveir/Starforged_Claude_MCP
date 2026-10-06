namespace StarForged_Claude_MCP.Shared.Results;

public abstract class ErrorResult<TErrorType>
{
    public static ErrorResult<TErrorType> Succeed() => new ErrorSuccessResult<TErrorType>();

    public static ErrorResult<TErrorType> Fail(TErrorType value) => new ErrorFailureResult<TErrorType>(value);

    public static Task<ErrorResult<TErrorType>> FailAsTask(TErrorType value) =>
        Task.FromResult<ErrorResult<TErrorType>>(new ErrorFailureResult<TErrorType>(value));

    public void Act(
        Action onSuccess,
        Action<TErrorType> onFailure)
    {
        switch (this)
        {
            case ErrorSuccessResult<TErrorType>:
                onSuccess();
                break;

            case ErrorFailureResult<TErrorType> failure:
                onFailure(failure.Error);
                break;
        }
    }

    public TReturnType Map<TReturnType>(
        Func<TReturnType> onSuccess,
        Func<TErrorType, TReturnType> onFailure)
    {
        return this switch
        {
            ErrorSuccessResult<TErrorType> => onSuccess(),
            ErrorFailureResult<TErrorType> failure => onFailure(failure.Error),
            _ => throw new InvalidOperationException("Unknown result type")
        };
    }
}

public class ErrorSuccessResult<TErrorType> : ErrorResult<TErrorType>
{ }

public class ErrorFailureResult<TErrorType> : ErrorResult<TErrorType>
{
    public TErrorType Error { get; }

    public ErrorFailureResult(TErrorType error)
    {
        Error = error;
    }
}
