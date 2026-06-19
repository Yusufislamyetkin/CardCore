namespace MiniBank.Shared.Domain.Primitives;

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value) { IsSuccess = true; _value = value; Error = null; }
    private Result(Error error) { IsSuccess = false; _value = default; Error = error; }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value of a failed result.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string entity, string id)
        => new($"{entity.ToUpperInvariant()}_NOT_FOUND", $"{entity} with ID '{id}' was not found.");

    public static Error Validation(string code, string message) => new(code, message);
    public static Error Conflict(string code, string message) => new(code, message);
}
