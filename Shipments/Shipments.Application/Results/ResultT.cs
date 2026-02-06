namespace Shipments.Application.Results;

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, ResultError? error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public static Result<T> Ok(T value) => new(true, value, null);

    public static new Result<T> Fail(string code, string message, IReadOnlyDictionary<string, object?>? meta = null)
        => new(false, default, new ResultError(code, message, meta));
}