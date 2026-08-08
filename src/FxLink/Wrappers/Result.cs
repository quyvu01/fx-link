using FxLink.Faults;

namespace FxLink.Wrappers;

/// <summary>
/// Represents a unified response wrapper for FxMap request/reply operations.
/// way to handle both successful responses and error scenarios.
/// </summary>
public class Result<T>
{
    /// <summary>
    /// Gets whether the request was processed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    public T Data { get; set; }

    /// <summary>
    /// Gets the fault information when the request failed.
    /// Will be null when <see cref="IsSuccess"/> is true.
    /// </summary>
    public Fault Fault { get; init; }

    /// <summary>
    /// Creates a successful response with the given data.
    /// </summary>
    /// <param name="data">The response data.</param>
    /// <returns>A successful FxMapResponse containing the data.</returns>
    public static Result<T> Success(T data) => new()
    {
        IsSuccess = true,
        Fault = null,
        Data = data
    };

    /// <summary>
    /// Creates a failed response with fault information.
    /// </summary>
    /// <param name="fault">The fault information.</param>
    /// <returns>A failed FxMapResponse containing the fault.</returns>
    public static Result<T> Failed(Fault fault) => new()
    {
        IsSuccess = false,
        Fault = fault
    };

    /// <summary>
    /// Creates a failed response from an exception.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="faultedMessageId">Optional identifier for the faulted message.</param>
    /// <returns>A failed FxMapResponse containing the fault information.</returns>
    public static Result<T> Failed(Exception exception, string faultedMessageId = null) => new()
    {
        IsSuccess = false,
        Fault = Fault.FromException(exception, faultedMessageId)
    };
}