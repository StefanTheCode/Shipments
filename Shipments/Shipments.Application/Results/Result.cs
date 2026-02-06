namespace Shipments.Application.Results;

public class Result
{
    public bool IsSuccess { get; }
    public ResultError? Error { get; }

    protected Result(bool isSuccess, ResultError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Ok() => new(true, null);

    public static Result Fail(string code, string message, IReadOnlyDictionary<string, object?>? meta = null)
        => new(false, new ResultError(code, message, meta));
}
