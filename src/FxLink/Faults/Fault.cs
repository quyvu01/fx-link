using FxLink.Abstractions.Contexts;

namespace FxLink.Faults;

public class Fault
{
    protected const int MaxExceptionDepth = 16;
    public string FaultId { get; set; }
    public string FaultedMessageId { get; set; }
    public DateTime Timestamp { get; set; }
    public ExceptionInfo[] Exceptions { get; set; }
    public IHostInfo Host { get; set; }

    public static Fault FromException(Exception exception, string faultedMessageId = null)
    {
        var exceptions = new List<ExceptionInfo>();
        var currentException = exception;
        var depth = 0;

        while (currentException != null && depth < MaxExceptionDepth)
        {
            exceptions.Add(new ExceptionInfo
            {
                ExceptionType = currentException.GetType().FullName,
                Message = currentException.Message,
                StackTrace = currentException.StackTrace,
                Source = currentException.Source
            });
            currentException = currentException.InnerException;
            depth++;
        }

        return new Fault
        {
            FaultId = Guid.NewGuid().ToString(),
            FaultedMessageId = faultedMessageId,
            Timestamp = DateTime.UtcNow,
            Exceptions = exceptions.ToArray(),
            Host = HostInfo.Current
        };
    }

    public Exception ToException()
    {
        if (Exceptions is not { Length: > 0 })
            return null;

        // Build exception chain from innermost to outermost
        Exception innerException = null;
        for (var i = Exceptions.Length - 1; i >= 0; i--)
        {
            var exInfo = Exceptions[i];
            innerException = new FaultException(exInfo, innerException);
        }

        return innerException;
    }
}

public sealed class Fault<T>(T message) : Fault
{
    public T Message { get; } = message;

    public new Fault<T> FromException(Exception exception, string faultedMessageId = null)
    {
        var exceptions = new List<ExceptionInfo>();
        var currentException = exception;
        var depth = 0;

        while (currentException != null && depth < MaxExceptionDepth)
        {
            exceptions.Add(new ExceptionInfo
            {
                ExceptionType = currentException.GetType().FullName,
                Message = currentException.Message,
                StackTrace = currentException.StackTrace,
                Source = currentException.Source
            });
            currentException = currentException.InnerException;
            depth++;
        }

        return new Fault<T>(Message)
        {
            FaultId = Guid.NewGuid().ToString(),
            FaultedMessageId = faultedMessageId,
            Timestamp = DateTime.UtcNow,
            Exceptions = exceptions.ToArray(),
            Host = HostInfo.Current
        };
    }
}

/// <summary>
/// Represents a reconstructed exception from fault information.
/// </summary>
public sealed class FaultException : Exception
{
    /// <summary>
    /// Gets the original exception type name.
    /// </summary>
    public string OriginalExceptionType { get; }

    /// <summary>
    /// Gets the original stack trace.
    /// </summary>
    public string OriginalStackTrace { get; }

    /// <summary>
    /// Gets the original source.
    /// </summary>
    public string OriginalSource { get; }

    /// <summary>
    /// Creates a new FaultException from exception info.
    /// </summary>
    public FaultException(ExceptionInfo exceptionInfo, Exception innerException = null)
        : base(exceptionInfo?.Message ?? "Unknown error", innerException)
    {
        OriginalExceptionType = exceptionInfo?.ExceptionType;
        OriginalStackTrace = exceptionInfo?.StackTrace;
        OriginalSource = exceptionInfo?.Source;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var result = $"{OriginalExceptionType ?? GetType().FullName}: {Message}";
        if (!string.IsNullOrEmpty(OriginalStackTrace))
            result += Environment.NewLine + OriginalStackTrace;
        return result;
    }
}

/// <summary>
/// Represents exception information within a fault.
/// </summary>
public sealed class ExceptionInfo
{
    /// <summary>
    /// Gets or sets the full type name of the exception.
    /// </summary>
    public string ExceptionType { get; set; }

    /// <summary>
    /// Gets or sets the exception message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the stack trace of the exception.
    /// </summary>
    public string StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the source of the exception.
    /// </summary>
    public string Source { get; set; }
}