namespace ClothingStore.Catalog.ApiClients;

/// <summary>Api answer: a value, or an error message that can be shown to the customer.</summary>
public sealed class ApiResult<T>
{
    private ApiResult(bool succeeded, T? value, string? error)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
    }

    public bool Succeeded { get; }
    public T? Value { get; }
    public string? Error { get; }

    public static ApiResult<T> Ok(T value) => new(true, value, null);
    public static ApiResult<T> Fail(string error) => new(false, default, error);
}

/// <summary>Placeholder type for calls that return no body.</summary>
public readonly record struct None;

/// <summary>The customer token was rejected (expired / logged out elsewhere). Handled by <see cref="Infrastructure.ApiUnauthorizedFilter"/>.</summary>
public sealed class ApiUnauthorizedException() : Exception("The API rejected the customer token.");
