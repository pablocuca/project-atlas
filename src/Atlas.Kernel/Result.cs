namespace Atlas.Kernel;

// Result<T> for expected failures; exceptions for bugs (docs/05-engineering/02-coding-standards.md §2).
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly DomainError? _error;

    private Result(T? value, DomainError? error, bool isSuccess)
    {
        _value = value;
        _error = error;
        IsSuccess = isSuccess;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value of a failed Result. Error: {_error!.Code}.");

    public DomainError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error of a successful Result.");

    public static Result<T> Ok(T value) => new(value, null, true);

    public static Result<T> Fail(DomainError error) => new(default, error, false);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<DomainError, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

    public static Result<T> Fail<T>(DomainError error) => Result<T>.Fail(error);
}
